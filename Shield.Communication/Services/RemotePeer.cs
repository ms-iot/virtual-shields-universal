using System;
using Windows.Networking;
using Windows.Storage.Streams;

namespace Shield.Communication.Services
{
    public class RemotePeer
    {
        IOutputStream outputStream;
        public HostName HostName { get; set; }
        
        public string IP { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public string Port { get; set; }

        public RemotePeer(IOutputStream outputStream, HostName hostName, String port)
        {
            this.outputStream = outputStream;
            this.HostName = hostName;
            this.Port = port;
        }

        public bool IsMatching(HostName hostName, String port)
        {
            return (this.HostName == hostName && this.Port == port);
        }

        public IOutputStream OutputStream
        {
            get { return outputStream; }
        }

        public string Message { get; set; }
        public DateTime Pinged { get; set; }
        public string OriginalPort { get; set; }

        public override String ToString()
        {
            return HostName + Port;
        }
    }
}