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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Shield.Communication.Services
{
    public class PrioritizedMessage
    {
        public string Message { get; set; }
        public int Priority { get; set; }
    }

    public class ServiceBase : IDisposable
    {
        internal StreamSocket socket;
        internal DataWriter dataWriter;
        internal DataReader dataReader;
        internal bool isListening = false;
        internal bool isPollingToSend = false;

        public delegate void CharReceivedHandler(char message);
        public event CharReceivedHandler CharReceived;
        public int CharEventHandlerCount = 0;
        internal Dictionary<string, Connection> clients = new Dictionary<string, Connection>();

        public bool IsClearToSend { get; set; }

        public delegate void OnConnectHandler(Connection newConnection);

        public event OnConnectHandler OnConnect;
        public event OnConnectHandler OnDisconnected;

        private Connection currentConnection = null;
        internal bool isPrePairedDevice = false;

        //todo: Add a set of priorities with timestamps - send in order of : priority + oldest msg.
        private Dictionary<string, PrioritizedMessage> queuedMessages = new Dictionary<string, PrioritizedMessage>();
        private Queue<string> queuedSends = new Queue<string>(); 

        public void Initialize(bool isPrePairedDevice)
        {
            if (socket != null)
            {
                socket.Dispose();
            }

            socket = new StreamSocket();

            this.isPrePairedDevice = isPrePairedDevice;
        }

        public void Terminate()
        {
            isListening = false;
            this.Dispose();
        }

        public void SetClient(string name, Connection connection)
        {
            clients[name] = connection;
        }

        public void ClearClient(string name)
        {
            if (name == null)
            {
                clients.Clear();
                return;
            }

            if (clients.ContainsKey(name))
            {
                clients.Remove(name);
            }
        }

        public virtual Task<Connections> GetConnections()
        {
            return new Task<Connections>(() => null);
        }

        public void Disconnect(Connection connection)
        {
            Terminate();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async Task<bool> Connect(Connection newConnection)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            this.currentConnection = newConnection;
            this.OnConnect?.Invoke(newConnection);
            return false;
        }

        internal bool InstrumentSocket(StreamSocket socket)
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
            catch (Exception e)
            {
                // socket failure can be recovered
                Debug.WriteLine(e.Message);
            }

            return result;
        }

        public async void ReceiveMessages()
        {
            try
            {
                while (isListening)
                {
                    uint sizeFieldCount = 0;
                    try
                    {
                        sizeFieldCount = await dataReader.LoadAsync(1);
                    }
                    catch (Exception e)
                    {
                        // ignore normal socket disconnections
                        if (e.HResult != -2147023901)
                        {
                            throw;
                        }

                        continue;
                    }

                    if (sizeFieldCount != 1)
                    {
                        isListening = false;
                        this.OnDisconnected?.Invoke(this.currentConnection);
                        break;
                    }

                    uint val = dataReader.ReadByte();
                    if (val < 255)
                    {
                        CharReceived?.Invoke((char)val);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                this.Terminate();
            }
        }

        private int msBetweenSends = 10;
        private DateTime nextSend = DateTime.Now;

        public async void SendMessages()
        {
            try
            {
                while (isListening)
                {
                    if (this.IsClearToSend)
                    {
                        string part = null;

                        lock (queuedMessages)
                        {
                            if (this.queuedSends.Count > 0)
                            {
                                part = this.queuedSends.Dequeue();
                            }
                            else if (this.queuedMessages.Count > 0)
                            {
                                var item = this.queuedMessages.FirstOrDefault(p => p.Value.Priority <= 10);
                                if (item.Value == null)
                                {
                                    item = this.queuedMessages.First();
                                }

                                var key = item.Key;
                                var value = item.Value;
                                this.queuedMessages.Remove(key);

                                var head = 0;
                                var tail = 0;
                                var length = value.Message.Length;

                                while (head < length)
                                {
                                    tail += 60;
                                    this.queuedSends.Enqueue(tail < length ? value.Message.Substring(head, 60) : value.Message.Substring(head));
                                    head += 60;
                                }
                            }
                        }

                        if (part != null)
                        {
                            Debug.WriteLine(part);
                            await SendMessage2(part);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                this.Terminate();
            }
        }

        public async Task SendMessage(string data, string key = null, int priority = 10)
        {
            if (!isPollingToSend)
            {
                await SendMessage2(data);
            }

            key = key ?? "?";

            lock (queuedMessages)
            {
                this.queuedMessages[key] = new PrioritizedMessage {Message = data, Priority = priority};
            }
        }

        public async Task SendMessage2(string data)
        {
            if (dataWriter != null)
            {
                Debug.WriteLine("Sending: " + data);
                dataWriter.WriteString(data);
                await dataWriter.StoreAsync();
                await dataWriter.FlushAsync();
            }
        }

        public virtual void Dispose()
        {
            if (socket != null)
            {
                socket?.Dispose();
                currentConnection = null;
                isListening = false;
                socket = null;
            }
        }

        public virtual void ListenForBeacons()
        {
        }
    }
}
