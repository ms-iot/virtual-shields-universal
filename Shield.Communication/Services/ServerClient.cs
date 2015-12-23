/*
    Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.

    The MIT License(MIT)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/
namespace Shield.Communication.Services
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.Networking;
    using Windows.Networking.Sockets;
    using Windows.Storage.Streams;

    public class ServerClient : ServiceBase
    {
        private readonly string remoteHost = string.Empty;

        private readonly string remoteService = string.Empty;

        public ServerClient(string rHost, string rService)
        {
            this.remoteHost = rHost;
            this.remoteService = rService;
        }

        public override async Task<Connections> GetConnections()
        {
            var remoteHostName = new HostName(this.remoteHost);
            var endpointList = await DatagramSocket.GetEndpointPairsAsync(remoteHostName, this.remoteService);
            var epair = endpointList.First();

            var connections = new Connections { new Connection("Server", epair) };

            return connections;
        }

        public override Task<bool> Connect(Connection newConnection)
        {
            // this re-enables after the disconnect scenario
            if (this.socket == null)
            {
                this.socket = new StreamSocket();
            }

            var peer = newConnection.Source as EndpointPair;
            return this.Connect(peer);
        }

        private async Task<bool> Connect(EndpointPair epair)
        {
            if (this.socket != null)
            {
                try
                {
                    await this.socket.ConnectAsync(epair);
                    this.dataReader = new DataReader(this.socket.InputStream);
                    this.dataReader.InputStreamOptions = InputStreamOptions.Partial;
#pragma warning disable 4014
                    Task.Run(() => { this.ReceiveMessages(); });
#pragma warning restore 4014
                    this.dataWriter = new DataWriter(this.socket.OutputStream);
                    return true;
                }
                catch (Exception)
                {
                    // multiple messages across protocols, all valid for not connecting
                }
            }

            return false;
        }
    }
}