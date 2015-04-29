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

        public bool IsClearToSend { get; set; }

        public delegate void OnConnectHandler(Connection newConnection);

        public event OnConnectHandler OnConnect;
        public event OnConnectHandler OnDisconnected;

        private Connection currentConnection = null;
        internal bool isPrePairedDevice = false;

        //JIM: Add a set of priorities with timestamps - send in order of : priority + oldest msg.
        private Dictionary<string, PrioritizedMessage> queuedMessages = new Dictionary<string, PrioritizedMessage>();
        private Queue<string> queuedSends = new Queue<string>(); 

        public void Initialize(bool isPrePairedDevice)
        {
            socket = new StreamSocket();
            this.isPrePairedDevice = isPrePairedDevice;
        }

        public void Terminate()
        {
            this.Dispose();
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

        public async void ReceiveMessages()
        {
            try
            {
                while (isListening)
                {
                    uint sizeFieldCount = await dataReader.LoadAsync(1);
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
            }
        }

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
                                this.IsClearToSend = false;
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
    }
}
