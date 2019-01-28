# EntityStore.Net

## Description

EntityStore.Net is a .NET Utility Class used with an [Event Store](https://eventstore.org/) backend. It is able to store and retrieve entities (C# objects) as well as their associated property changes.

## Installation

Package is available through [NuGet](https://www.nuget.org/packages/EntityStore.Net).

```
Install-Package EntityStore.Net
```

## Usage

All methods are provided from an EntityStore class. The contructor of this class takes a ConnectionOptions object with the following properties:

```cs
public class ConnectionOptions
{
    public string HostAddress { get; set; }
    public int StreamPort { get; set; }
    public int HttpPort { get; set; }
    public UserCredentials UserCredentials { get; set; }
    public ILogger Logger { get; set; }
}
```

Notes:
* HostAddress can either be a DNS name or IP Address.
* StreamPort and HttpPort are defined from the configuration of the instance.
* The UserCredentials property is a string username/password that has access to the instance.
* The ILogger type property is actually a requirement of the ProjectionsManager object defined by the main API provided by EventStore. It has the following properties:

```cs
public interface ILogger
{
    void Debug(string format, params object[] args);
    void Debug(Exception ex, string format, params object[] args);
    void Error(string format, params object[] args);
    void Error(Exception ex, string format, params object[] args);
    void Info(string format, params object[] args);
    void Info(Exception ex, string format, params object[] args);
}
```

The EntityStore class is defined with the following methods:

#### Contructor

```cs
public EntityStore(ConnectionOptions connectionOptions) { }
```

#### Methods

```cs
// Insert a new Entity of type T into the Store. Returns the new stream name of the instered entity.
public string InsertNewEntity<T>(T entity) where T : class { }

// Update an existing Entity of type T in the Store given the stream name provided by the Insert method.
public void UpdateExistingEntity<T>(T entity, string streamName) where T : class { }

// Gets the Entity at the current moment from the given stream name. Will return null if the stream does not exist.
public T GetCurrentEntity<T>(string streamName) where T : class { }

// Gets the Entity at the current moment from the given stream name with History data. Will return null if the stream does not exist.
public EntityWithHistory<T> GetCurrentEntityWithHistory<T>(string streamName) where T : class { }

// Deletes an Entity from the Event Store based on the stream name.
public void DeleteEntityStream(string streamName) { }

// Gets a list of all entities of type T.
public IEnumerable<T> Entities<T>() where T : class { }
```
