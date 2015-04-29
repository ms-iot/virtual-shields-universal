using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Shield.Communication.Services
{
    public class ServerClient : ServiceBase
    {
        private string remoteHost = "";
        private string remoteService = "";
        
        public ServerClient(string rHost, string rService)
        {
          this.remoteHost = rHost;
          this.remoteService = rService;
        }

        public override async Task<Connections> GetConnections()
        {
            var remoteHostName = new HostName(remoteHost);
            var endpointList = await DatagramSocket.GetEndpointPairsAsync(remoteHostName, remoteService);
            var epair = endpointList.First();

            var connections = new Connections {new Connection("Server", epair)};

            return connections;
        }

        public override Task<bool> Connect(Connection newConnection)
        {
            // this re-enables after the disconnect scenario
            if (socket == null)
            {
                socket = new StreamSocket();
            }

            var peer = newConnection.Source as EndpointPair;
            return Connect(peer);
        }

        private async Task<bool> Connect(EndpointPair epair)
        {
            if (socket != null)
            {
                try
                {
                    await socket.ConnectAsync(epair);
                    dataReader = new DataReader(socket.InputStream);
                    dataReader.InputStreamOptions = InputStreamOptions.Partial;
#pragma warning disable 4014
                    Task.Run(() => { ReceiveMessages(); });
#pragma warning restore 4014
                    dataWriter = new DataWriter(socket.OutputStream);
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }
    }
}
