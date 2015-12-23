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
    using System.Threading.Tasks;

    using Windows.Devices.Enumeration;
    using Windows.Devices.SerialCommunication;

    public class USB : ServiceBase
    {
        public USB()
        {
            this.isPollingToSend = true;
        }

        public SerialDevice service { get; private set; }

        public override async Task<Connections> GetConnections()
        {
            var identifyingSubstring = "VID_2341";

            try
            {
                var devices = SerialDevice.GetDeviceSelector();
                var peers = await DeviceInformation.FindAllAsync(devices);

                var connections = new Connections();
                foreach (var peer in peers)
                {
                    if (peer.Name.Contains(identifyingSubstring) || peer.Id.Contains(identifyingSubstring))
                    {
                        connections.Add(new Connection(peer.Name, peer));
                    }
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
            var deviceInfo = newConnection.Source as DeviceInformation;
            if (deviceInfo != null)
            {
                this.service = await SerialDevice.FromIdAsync(deviceInfo.Id);
                if (this.service == null)
                {
                    return false;
                }

                this.service.BaudRate = 115200;
                this.service.StopBits = SerialStopBitCount.One;
                this.service.Handshake = SerialHandshake.None;
                this.service.DataBits = 8;

                this.service.ReadTimeout = TimeSpan.FromSeconds(5);
                this.service.WriteTimeout = TimeSpan.FromSeconds(5);
                this.service.IsDataTerminalReadyEnabled = false;

                return this.InstrumentSocket(this.service.InputStream, this.service.OutputStream);
            }

            return false;
        }

        public override void Dispose()
        {
            this.service?.Dispose();
            base.Dispose();
            this.service = null;
        }
    }
}