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

using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Connectivity;

namespace Shield.Communication.Services
{
    public class Wifi : ServiceBase
    {
        private int port = 1235;
        public Wifi()
        {
            isPollingToSend = true;
        }

        public override async Task<Connections> GetConnections()
        {
            var connections = new Connections();
            connections.Add(new Connection("Arduino", "192.168.173.17"));
            await Task.Delay(0);
            return connections;
        }

        public override async Task<bool> Connect(Connection newConnection)
        {
            //purposely connect to a destination
            HostName hostName = null;
            string remoteServiceName = null;

            var peer = newConnection.Source as PeerInformation;
            if (peer != null)
            {
                hostName = peer.HostName;
                remoteServiceName = "1";
            }
            else
            {
                var deviceInfo = newConnection.Source as DeviceInformation;
                if (deviceInfo != null)
                {
                    var service = await RfcommDeviceService.FromIdAsync(deviceInfo.Id);
                    hostName = service.ConnectionHostName;
                    remoteServiceName = service.ConnectionServiceName;
                }
                else
                {
                    var ip = newConnection.Source as string;
                    if (ip != null)
                    {
                        hostName = new HostName(ip);
                        remoteServiceName = "1235";
                    }
                }
            }

            bool result = false;

            if (hostName != null)
            {
                result = await Connect(hostName, remoteServiceName);
                await base.Connect(newConnection);
            }

            return result;
        }

        private bool InstrumentSocket(StreamSocket socket)
        {
            var result = false;

            try
            {
                dataReader = new DataReader(socket.InputStream);
                this.isListening = true;
#pragma warning disable 4014
                Task.Run(() => { ReceiveMessages(); });
                Task.Run(() => { SendMessages(); });
#pragma warning restore 4014
                dataWriter = new DataWriter(socket.OutputStream);
                result = true;
            }
            catch (Exception)
            {
                //log
            }

            return result;
        }

        private async Task<bool> Connect(HostName deviceHostName, string remoteServiceName)
        {
            if (!isListening)
            {
                if (socket == null)
                {
                    socket = new StreamSocket();
                }

                if (socket != null)
                {
                    try
                    {
                        CancellationTokenSource cts = new CancellationTokenSource();
                        cts.CancelAfter(10000);
                        await socket.ConnectAsync(deviceHostName, remoteServiceName);
                        return InstrumentSocket(socket);
                    }
                    catch (Exception e)
                    {
                        //log
                    }
                }

                return false;
            }
            else
            {
                return true;
            }
        }
    }
}