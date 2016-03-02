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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.Networking.Sockets;
    using Windows.Storage.Streams;

    public class PrioritizedMessage
    {
        public string Message { get; set; }

        public int Priority { get; set; }
    }

    public class ServiceBase : IDisposable
    {
        public delegate void CharReceivedHandler(char message);

        public delegate void OnConnectHandler(Connection newConnection);

        public delegate void ThreadedExceptionHandler(Exception exception);

        // todo: Add a set of priorities with timestamps - send in order of : priority + oldest msg.
        private readonly Dictionary<string, PrioritizedMessage> queuedMessages =
            new Dictionary<string, PrioritizedMessage>();

        private readonly Queue<string> queuedSends = new Queue<string>();

        public int CharEventHandlerCount = 0;

        internal Dictionary<string, Connection> clients = new Dictionary<string, Connection>();

        private Connection currentConnection;

        internal DataReader dataReader;

        internal DataWriter dataWriter;

        private bool isFlushImplemented = true;

        internal bool isListening;

        internal bool isPollingToSend = false;

        internal bool isPrePairedDevice;

        private int msBetweenSends = 10;

        private DateTime nextSend = DateTime.Now;

        internal StreamSocket socket;

        public bool IsClearToSend { get; set; }

        public bool IsConnected { get; set; }

        public virtual void Dispose()
        {
            if (this.socket != null)
            {
                this.socket?.Dispose();
                this.currentConnection = null;
                this.isListening = false;
                this.socket = null;
                this.isFlushImplemented = true;
            }
        }

        public event ThreadedExceptionHandler ThreadedException;

        public event CharReceivedHandler CharReceived;

        public event OnConnectHandler OnConnect;

        public event OnConnectHandler OnDisconnected;

        public void Initialize(bool isPrePairedDevice)
        {
            this.socket?.Dispose();

            this.socket = new StreamSocket();

            this.isPrePairedDevice = isPrePairedDevice;
        }

        public void Terminate()
        {
            this.isListening = false;
            this.Dispose();
        }

        public void OnThreadedException(Exception e)
        {
            this.ThreadedException?.Invoke(e);
        }

        public void SetClient(string name, Connection connection)
        {
            this.clients[name] = connection;
        }

        public void ClearClient(string name)
        {
            if (name == null)
            {
                this.clients.Clear();
                return;
            }

            if (this.clients.ContainsKey(name))
            {
                this.clients.Remove(name);
            }
        }

        public virtual Task<Connections> GetConnections()
        {
            return new Task<Connections>(() => null);
        }

        public virtual void Disconnect(Connection connection)
        {
            this.IsConnected = false;
            this.OnDisconnected?.Invoke(connection);
            this.Terminate();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async Task<bool> Connect(Connection newConnection)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            this.currentConnection = newConnection;
            this.OnConnect?.Invoke(newConnection);
            this.IsConnected = true;
            return false;
        }

        internal bool InstrumentSocket(StreamSocket socket)
        {
            return this.InstrumentSocket(socket.InputStream, socket.OutputStream);
        }

        internal bool InstrumentSocket(IInputStream input, IOutputStream output)
        {
            var result = false;

            try
            {
                this.dataReader = new DataReader(input);
                this.isListening = true;
#pragma warning disable 4014
                Task.Run(() => { this.ReceiveMessages(); });
                Task.Run(() => { this.SendMessages(); });
#pragma warning restore 4014
                this.dataWriter = new DataWriter(output);
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
                while (this.isListening)
                {
                    uint sizeFieldCount = 0;
                    try
                    {
                        sizeFieldCount = await this.dataReader.LoadAsync(1);
                    }
                    catch (UnsupportedSensorException use)
                    {
                        Debug.WriteLine("UnsupportedSensorException: " + use);

                        // send message to remote
                        this.ThreadedException?.Invoke(use);
                    }
                    catch (Exception e)
                    {
                        // ignore normal socket disconnections
                        // -2147023901 == The I/O operation has been aborted because of either a thread exit or an application request. 
                        if (e.HResult != -2147023901)
                        {
                            Debug.WriteLine("Socket exception: " + e);
                            this.ThreadedException?.Invoke(e);
                            throw;
                        }

                        Debug.WriteLine("Socket disconnect: " + e);
                        this.ThreadedException?.Invoke(e);
                        continue;
                    }

                    if (sizeFieldCount != 1)
                    {
                        this.Disconnect(this.currentConnection);
                        break;
                    }

                    uint val = this.dataReader.ReadByte();
                    if (val < 255)
                    {
                        //Debug.Write(val + ",");
                        Debug.Write((char)val);
                        this.CharReceived?.Invoke((char)val);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReceiveMessages Fatal Exception: " + ex);
                this.Terminate();
            }
        }

        public async void SendMessages()
        {
            try
            {
                while (this.isListening)
                {
                    if (this.IsClearToSend)
                    {
                        string part = null;

                        lock (this.queuedMessages)
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
                                    this.queuedSends.Enqueue(
                                        tail < length
                                            ? value.Message.Substring(head, 60)
                                            : value.Message.Substring(head));
                                    head += 60;
                                }
                            }
                        }

                        if (part != null)
                        {
                            Debug.WriteLine(part);
                            await this.SendMessage2(part);
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
            if (!this.isPollingToSend)
            {
                await this.SendMessage2(data);
            }

            key = key ?? "?";

            lock (this.queuedMessages)
            {
                this.queuedMessages[key] = new PrioritizedMessage { Message = data, Priority = priority };
            }
        }

        public async Task SendMessage2(string data)
        {
            if (this.dataWriter != null)
            {
                Debug.WriteLine("Sending: " + data);
                this.dataWriter.WriteString(data);
                await this.dataWriter.StoreAsync();

                if (this.isFlushImplemented)
                {
                    try
                    {
                        await this.dataWriter.FlushAsync();
                    }
                    catch (NotImplementedException)
                    {
                        this.isFlushImplemented = false;
                    }
                }
            }
        }

        public virtual void ListenForBeacons()
        {
        }
    }
}