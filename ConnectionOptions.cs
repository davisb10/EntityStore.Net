using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;

namespace EntityStore.Net
{
    public class ConnectionOptions
    {
        public string HostAddress { get; set; }

        public int StreamPort { get; set; }

        public int HttpPort { get; set; }

        public UserCredentials UserCredentials { get; set; }

        public ILogger Logger { get; set; }
    }
}
