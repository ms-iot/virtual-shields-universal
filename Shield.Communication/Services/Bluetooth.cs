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

﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Shield.Communication.Services
{
    public class Bluetooth : ServiceBase
    {
        public Bluetooth()
        {
            isPollingToSend = true;
        }

        public override async Task<Connections> GetConnections()
        {
            if (isPrePairedDevice)
            {
                PeerFinder.AlternateIdentities["Bluetooth:Paired"] = "";
            }

            try
            {
                PeerFinder.AllowBluetooth = true;
                PeerFinder.AllowWiFiDirect = true;
                PeerFinder.DisplayName = "Virtual Shields";
                PeerFinder.Role = PeerRole.Peer;
                if (!isPrePairedDevice)
                {
                    PeerFinder.Start();
                }

                var peers = await PeerFinder.FindAllPeersAsync();
                var connections = new Connections();
                foreach (var peer in peers)
                {
                    connections.Add(new Connection(peer.DisplayName, peer));
                }

                return connections;
            }
            catch (Exception)
            {
                return null;
            }

        }

        public override async Task<bool> Connect(Connection newConnection)
        {
            var peer = newConnection.Source as PeerInformation;
            var result = await Connect(peer.HostName);
            await base.Connect(newConnection);
            return result;
        }

        private async Task<bool> Connect(HostName deviceHostName)
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
                        await socket.ConnectAsync(deviceHostName, "1");
                        dataReader = new DataReader(socket.InputStream);
                        this.isListening = true;
#pragma warning disable 4014
                        Task.Run(() => { ReceiveMessages(); });
                        Task.Run(() => { SendMessages(); });
#pragma warning restore 4014
                        dataWriter = new DataWriter(socket.OutputStream);

                        return true;
                    }
                    catch (Exception)
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