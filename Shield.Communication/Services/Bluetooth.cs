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
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Devices.Bluetooth.Rfcomm;
    using Windows.Devices.Enumeration;
    using Windows.Networking;
    using Windows.Networking.Proximity;
    using Windows.Networking.Sockets;

    public class Bluetooth : ServiceBase
    {
        public Bluetooth()
        {
            this.isPollingToSend = true;
        }

        public override async Task<Connections> GetConnections()
        {
            if (this.isPrePairedDevice)
            {
                PeerFinder.AlternateIdentities["Bluetooth:Paired"] = string.Empty;
            }

            try
            {
                var devices = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                var peers = await DeviceInformation.FindAllAsync(devices);

                var connections = new Connections();
                foreach (var peer in peers)
                {
                    connections.Add(new Connection(peer.Name, peer));
                }

                return connections;
            }
            catch (Exception e)
            {
                Debug.WriteLine("GetConnections: pairing failed: " + e);
                return null;
            }
        }

        public override async Task<bool> Connect(Connection newConnection)
        {
            var result = false;
            try
            {
                HostName hostName = null;
                string remoteServiceName = null;

                var peer = newConnection.Source as PeerInformation;
                if( peer != null )
                {
                    hostName = peer.HostName;
                    remoteServiceName = "1";
                }
                else
                {
                    var deviceInfo = newConnection.Source as DeviceInformation;
                    if( deviceInfo != null )
                    {
                        var service = await RfcommDeviceService.FromIdAsync(deviceInfo.Id);
                        if( service == null )
                        {
                            return false;
                        }

                        hostName = service.ConnectionHostName;
                        remoteServiceName = service.ConnectionServiceName;
                    }
                }

                if( hostName != null )
                {
                    result = await this.Connect(hostName, remoteServiceName);
                    await base.Connect(newConnection);
                }
            }
            catch (Exception)
            {
                //ignore bad connection, return false
            }

            return result;
        }

        private async Task<bool> Connect(HostName deviceHostName, string remoteServiceName)
        {
            if (!this.isListening)
            {
                if (this.socket == null)
                {
                    this.socket = new StreamSocket();
                }

                if (this.socket != null)
                {
                    try
                    {
                        var cts = new CancellationTokenSource();
                        cts.CancelAfter(10000);
                        await this.socket.ConnectAsync(deviceHostName, remoteServiceName);
                        return this.InstrumentSocket(this.socket);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Connect: connection failed: " + e);
                    }
                }

                return false;
            }

            return true;
        }
    }
}