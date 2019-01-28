using System;
using System.Collections.Generic;

namespace EntityStore.Net
{
    public class EntityWithHistory<T> where T : class
    {
        /// <summary>
        ///     Current Entity
        /// </summary>
        public T Entity { get; set; }

        /// <summary>
        ///     List of KeyValue Pairs representing the History.
        ///     <para />
        ///     Key: DateTime of the changes.
        ///     Value: List of Property Changes that happened.
        /// </summary>
        public List<KeyValuePair<DateTime, List<EntityPropertyChange>>> HistoryChanges { get; set; }
    }
}
