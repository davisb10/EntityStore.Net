using EntityStore.Net.HelperClasses;
using EntityStore.Net.Utilities;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Projections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Reflection;
using System.Text;

namespace EntityStore.Net
{
    public class EntityStore
    {
        private IEventStoreConnection _connection;

        private ProjectionsManager _projectionsManager;

        private ConnectionOptions _connectionOptions;

        private IPEndPoint _streamEndpoint;

        private IPEndPoint _httpEndpoint;

        private EntityStoreConnectionStatus _connectionStatus = EntityStoreConnectionStatus.Uninitialized;
        public EntityStoreConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
        }

        public enum EntityStoreConnectionStatus
        {
            Uninitialized,
            Connected,
            Failed,
            Disconnected
        }

        public EntityStore(ConnectionOptions connectionOptions)
        {
            IPHostEntry host = Dns.GetHostEntry(connectionOptions.HostAddress);

            if (host.AddressList.Length == 0)
            {
                _connectionStatus = EntityStoreConnectionStatus.Failed;
                return;
            }

            _streamEndpoint = new IPEndPoint(host.AddressList[0], connectionOptions.StreamPort);
            _httpEndpoint = new IPEndPoint(host.AddressList[0], connectionOptions.HttpPort);

            _connectionOptions = connectionOptions;

            _connection = EventStoreConnection.Create(_streamEndpoint);

            _connection.Connected += Connection_Connected_Event;
            _connection.Disconnected += Connection_Disconnected_Event;

            _connection.ConnectAsync().Wait();

            _projectionsManager = new ProjectionsManager(connectionOptions.Logger, _httpEndpoint, TimeSpan.FromSeconds(30));

            _projectionsManager.EnableAsync("$streams", _connectionOptions.UserCredentials).Wait();
        }

        private void Connection_Disconnected_Event(object sender, ClientConnectionEventArgs e)
        {
            _connectionStatus = EntityStoreConnectionStatus.Connected;
        }

        private void Connection_Connected_Event(object sender, ClientConnectionEventArgs e)
        {
            _connectionStatus = EntityStoreConnectionStatus.Disconnected;
        }

        /// <summary>
        ///     Insert a new Entity of type T into the Store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public string InsertNewEntity<T>(T entity) where T : class
        {
            if (typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length == 0)
                throw new ArgumentException("Entity provided must contain at least one Public or Instance Property.");

            if (typeof(T).Module.ScopeName == "CommonLanguageRuntimeLibrary")
                throw new ArgumentException("Entity provided can not be a CLR provided Data Type.");

            Guid streamGuid = Guid.NewGuid();
            string entityTypeName = typeof(T).Name;
            string streamName = $"{entityTypeName.ToLower()}-{streamGuid.ToString()}";

            Metadata metadata = new Metadata
            {
                StreamGuid = streamGuid,
                StreamDataType = entityTypeName,
                EventEntryDate = DateTime.Now
            };

            EventData eventData = new EventData(Guid.NewGuid(), entityTypeName, true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity)),
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata)));

            _connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, eventData).Wait();

            return streamName;
        }

        /// <summary>
        ///     Update an existing Entity of type T in the Store given the stream name provided by the Insert method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="streamName"></param>
        /// <exception cref="ArgumentException"></exception>
        public void UpdateExistingEntity<T>(T entity, string streamName) where T : class
        {
            if (typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length == 0)
                throw new ArgumentException("Entity provided must contain at least one Public or Instance Property.");

            if (typeof(T).Module.ScopeName == "CommonLanguageRuntimeLibrary")
                throw new ArgumentException("Entity provided can not be a CLR provided Data Type.");

            string entityType = typeof(T).Name;

            EventReadResult lastEvent = _connection.ReadEventAsync(streamName, StreamPosition.End, false).Result;

            string metadataString = Encoding.UTF8.GetString(lastEvent.Event.Value.Event.Metadata);
            Metadata metadata = JsonConvert.DeserializeObject<Metadata>(metadataString);

            T existingEntity = GetCurrentEntity<T>(streamName);
            Collection<EntityPropertyChange> propertyChanges = PropertyComparator.Compare(existingEntity, entity);

            metadata.EventEntryDate = DateTime.Now;

            EventData eventData = new EventData(Guid.NewGuid(), entityType, true,
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(propertyChanges)),
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata)));

            _connection.AppendToStreamAsync(streamName, ExpectedVersion.Any, eventData).Wait();
        }

        /// <summary>
        ///     Gets the Entity at the current moment from the given stream name.
        ///     <para />
        ///     Note: Will return null if the stream does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="streamName"></param>
        /// <returns></returns>
        public T GetCurrentEntity<T>(string streamName) where T : class
        {
            return GetCurrentEntityWithHistory<T>(streamName)?.Entity;
        }

        /// <summary>
        ///     Gets the Entity at the current moment from the given stream name with History data.
        ///     <para />
        ///     Note: Will return null if the stream does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="streamName"></param>
        /// <returns></returns>
        public EntityWithHistory<T> GetCurrentEntityWithHistory<T>(string streamName) where T : class
        {
            EntityWithHistory<T> entityWithHistory = new EntityWithHistory<T>();

            List<ResolvedEvent> streamEvents = new List<ResolvedEvent>();

            StreamEventsSlice currentSlice;
            long nextSliceStart = StreamPosition.Start;
            do
            {
                currentSlice = _connection.ReadStreamEventsForwardAsync(streamName, nextSliceStart, 200, false).Result;

                nextSliceStart = currentSlice.NextEventNumber;

                streamEvents.AddRange(currentSlice.Events);
            } while (!currentSlice.IsEndOfStream);

            // If the Stream has been deleted or the Stream was not found, return null.
            if (currentSlice.Status == SliceReadStatus.StreamDeleted || currentSlice.Status == SliceReadStatus.StreamNotFound)
            {
                return null;
            }

            // Stream Event Count = 1 when the Entity has only been entered and not updated.
            if (streamEvents.Count == 1)
            {
                RecordedEvent initialEvent = streamEvents[0].Event;

                entityWithHistory.Entity = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(initialEvent.Data));

                entityWithHistory.HistoryChanges = new List<KeyValuePair<DateTime, List<EntityPropertyChange>>>();
            }

            // Stream Event Count > 1 when the Entity has been entered and updated.
            if (streamEvents.Count > 1)
            {
                RecordedEvent initialEvent = streamEvents[0].Event;

                entityWithHistory.Entity = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(initialEvent.Data));
                entityWithHistory.HistoryChanges = new List<KeyValuePair<DateTime, List<EntityPropertyChange>>>();

                RecordedEvent updateEvent;
                List<EntityPropertyChange> propertyChanges;
                KeyValuePair<DateTime, List<EntityPropertyChange>> historyItem;
                Metadata metadata;
                Type entityType = typeof(T);

                PropertyInfo pi;
                for (int i = 1; i < streamEvents.Count; i++)
                {
                    updateEvent = streamEvents[i].Event;
                    propertyChanges = JsonConvert.DeserializeObject<List<EntityPropertyChange>>(Encoding.UTF8.GetString(updateEvent.Data));
                    metadata = JsonConvert.DeserializeObject<Metadata>(Encoding.UTF8.GetString(updateEvent.Metadata));

                    foreach (EntityPropertyChange propertyChange in propertyChanges)
                    {
                        pi = entityType.GetProperty(propertyChange.PropertyName);

                        pi.SetValue(entityWithHistory.Entity, Convert.ChangeType(propertyChange.NewValue, pi.PropertyType), null);
                    }

                    historyItem = new KeyValuePair<DateTime, List<EntityPropertyChange>>(metadata.EventEntryDate, propertyChanges);

                    entityWithHistory.HistoryChanges.Add(historyItem);
                }
            }

            return entityWithHistory;
        }

        /// <summary>
        ///     Deletes an Entity from the Event Store based on the stream name.
        /// </summary>
        /// <param name="streamName"></param>
        public void DeleteEntityStream(string streamName)
        {
            _connection.DeleteStreamAsync(streamName, ExpectedVersion.Any).Wait();
        }

        /// <summary>
        ///     Gets a list of all entities of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> Entities<T>() where T : class
        {
            string typeNameLower = typeof(T).Name.ToLower();
            string projectionName = $"streams-for-type-{typeNameLower}";

            string projectionQuery = Projections.PROJECTION_EVENT_TYPE.Replace("{{eventType}}", typeNameLower);

            _projectionsManager.CreateContinuousAsync(projectionName, projectionQuery, _connectionOptions.UserCredentials).Wait();

            string result = _projectionsManager.GetResultAsync(projectionName, _connectionOptions.UserCredentials).Result;

            StreamIdSearchResult searchResult = JsonConvert.DeserializeObject<StreamIdSearchResult>(result);

            _projectionsManager.DisableAsync(projectionName, _connectionOptions.UserCredentials).Wait();
            _projectionsManager.DeleteAsync(projectionName, _connectionOptions.UserCredentials).Wait();

            List<T> entities = new List<T>();

            foreach (string streamId in searchResult.StreamIds)
            {
                entities.Add(GetCurrentEntity<T>(streamId));
            }

            return entities;
        }
    }
}
