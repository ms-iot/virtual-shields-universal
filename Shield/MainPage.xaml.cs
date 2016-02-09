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
namespace Shield
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using Shield.Communication;
    using Shield.Communication.Destinations;
    using Shield.Communication.Services;
    using Shield.Core;
    using Shield.Core.Models;
    using Shield.Services;
    using Shield.ViewModels;

    using Windows.Devices.Enumeration;
    using Windows.Foundation.Metadata;
    using Windows.Networking;
    using Windows.System.Display;
    using Windows.UI.Core;
    using Windows.UI.ViewManagement;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class MainPage : Page
    {
        private const long pingCheck = 60 * 10000000; // 1min

        public static MainPage Instance;

        private readonly AppSettings appSettings;

        private readonly Stopwatch connectionStopwatch = new Stopwatch();

        internal readonly CoreDispatcher dispatcher;

        private readonly MainViewModel model;

        internal readonly Dictionary<string, int> sensors = new Dictionary<string, int>();

        private readonly Dictionary<string, ServiceBase> services = new Dictionary<string, ServiceBase>();

        private Audio audio;

        private Camera camera;

        public Connection currentConnection;

        private List<IDestination> destinations;

        private bool isCameraInitialized;

        public bool IsInSettings = false;

        private bool isRunning;

        private bool IsWelcomeMessageShowing = true;

        private DisplayRequest keepScreenOnRequest;

        private int lastConnection = -1;

        private Manager manager;

        private long nextPingCheck = DateTime.UtcNow.Ticks;

        private long recentStringReceivedTick;

        // Setup for ServerClient Connection
        public string remoteHost = "SERVER_IP_OR_ADDRESS";

        public string remoteService = "SERVER_PORT";

        private Screen screen;

        private long sentPingTick;

        public ServiceBase service;

        private Web web;

        public MainPage()
        {
            Instance = this;

            this.appSettings = (AppSettings)Application.Current.Resources["appSettings"];

            this.InitializeComponent();
            this.model = new MainViewModel();
            this.dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            this.Initialize();

            if (ApiInformation.IsTypePresent("Windows.Phone.UI.ViewManagement.StatusBar"))
            {
                if (StatusBar.GetForCurrentView() != null)
                {
                    StatusBar.GetForCurrentView().BackgroundOpacity = 0.50;
                }
            }
        }

        public Sensors Sensors { get; set; }

        public async void SetService()
        {
            if (this.appSettings.ConnectionIndex < 0)
            {
                return;
            }

            ServiceBase.OnConnectHandler OnConnection = async connection =>
                {
                    if (this.IsWelcomeMessageShowing)
                    {
                        this.IsWelcomeMessageShowing = false;
                        await
                            this.dispatcher.RunAsync(
                                CoreDispatcherPriority.Normal, 
                                () => { this.backgroundImage.ClearValue(Image.SourceProperty); });
                    }

                    await this.SendResult(new SystemResultMessage("CONNECT"), "!!");
                };

            ServiceBase.OnConnectHandler OnDisconnected = connection => { this.Disconnect(); };

            if (this.lastConnection != this.appSettings.ConnectionIndex)
            {
                await
                    this.dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, 
                        () => { this.appSettings.CurrentConnectionState = (int)ConnectionState.Disconnecting; });

                this.Disconnect();
                this.lastConnection = this.appSettings.ConnectionIndex;

                await
                    this.dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, 
                        () => { this.appSettings.CurrentConnectionState = (int)ConnectionState.NotConnected; });
            }

            if (this.service != null)
            {
                this.service.OnConnect -= OnConnection;
                this.service.OnDisconnected -= OnDisconnected;
            }

            switch (this.appSettings.ConnectionIndex)
            {
                case AppSettings.CONNECTION_BLUETOOTH:
                    {
                        this.service = this.services.ContainsKey("Bluetooth")
                                           ? this.services["Bluetooth"]
                                           : new Bluetooth();
                        this.services["Bluetooth"] = this.service;
                        App.Telemetry.Context.Properties["connection.type"] = "Bluetooth";

                        break;
                    }

                case AppSettings.CONNECTION_WIFI:
                case AppSettings.CONNECTION_MANUAL:
                    {
                        this.service = this.services.ContainsKey("Wifi")
                                           ? this.services["Wifi"]
                                           : new Wifi(AppSettings.BroadcastPort);
                        this.services["Wifi"] = this.service;
                        App.Telemetry.Context.Properties["connection.type"] = "Wifi";

                        if (this.appSettings.ConnectionIndex == 2
                            && !string.IsNullOrWhiteSpace(this.appSettings.Hostname))
                        {
                            this.service.SetClient(
                                "added", 
                                new Connection(
                                    "added", 
                                    new RemotePeer(
                                        null, 
                                        new HostName(this.appSettings.Hostname), 
                                        AppSettings.BroadcastPort.ToString())));
                        }

                        break;
                    }

                case AppSettings.CONNECTION_USB:
                    {
                        this.service = new USB();
                        App.Telemetry.Context.Properties["connection.type"] = "USB";
                        break;
                    }

                default:
                    {
                        throw new NotImplementedException(
                            "Connection type (" + this.appSettings.ConnectionIndex + ") not supported");
                    }
            }

            this.service.OnConnect += OnConnection;
            this.service.OnDisconnected += OnDisconnected;
            this.service.ThreadedException += this.Service_ThreadedException;

            if (!this.service.IsConnected)
            {
                this.service.Initialize( !this.appSettings.MissingBackButton );
            }

            this.service.ListenForBeacons();
            this.RefreshConnections();
        }

        private async void Service_ThreadedException(Exception exception)
        {
            if (exception is UnsupportedSensorException)
            {
                await this.SendResult(new ResultMessage { ResultId = -1, Result = "Unsupported Sensor" });
            }
        }

        private void Initialize()
        {
            this.destinations = new List<IDestination>();

            this.DataContext = this.model;
            this.model.Sensors = new Sensors(this.dispatcher);

            MessageFactory.LoadClasses();

            this.Sensors = this.model.Sensors;

            // the Azure BLOB storage helper is handed to both Camera and audio for stream options
            // if it is not used, no upload will to Azure BLOB will be done
            this.camera = new Camera();
            this.audio = new Audio();

            this.Sensors.OnSensorUpdated += this.Sensors_OnSensorUpdated;
            this.Sensors.Start();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            this.canvas.PointerPressed += async (s, a) => await this.SendEvent(s, a, "pressed");
            this.canvas.PointerReleased += async (s, a) => await this.SendEvent(s, a, "released");

            this.Display.PointerPressed += async (s, a) => await this.SendEvent(s, a, "pressed");
            this.Display.PointerReleased += async (s, a) => await this.SendEvent(s, a, "released");

            this.screen = new Screen(this);
            this.web = new Web(this);
            this.CheckAlwaysRunning();

            this.isRunning = true;
#pragma warning disable 4014
            Task.Run(
                () =>
                    {
                        this.SetService();
                        this.AutoReconnect();
                    });
            Task.Run(() => { this.ProcessMessages(); });
#pragma warning restore 4014
        }

        public void CheckAlwaysRunning(bool? force = null)
        {
            if ((force.HasValue && force.Value) || this.appSettings.AlwaysRunning)
            {
                this.keepScreenOnRequest = new DisplayRequest();
                this.keepScreenOnRequest.RequestActive();
            }
            else if (this.keepScreenOnRequest != null)
            {
                this.keepScreenOnRequest.RequestRelease();
                this.keepScreenOnRequest = null;
            }
        }

        private async void AutoReconnect()
        {
            var isConnecting = false;
            while (this.isRunning)
            {
                if (!isConnecting && this.currentConnection == null && !this.IsInSettings
                    && this.appSettings.AutoConnect)
                {
                    var previousConnection = this.appSettings.PreviousConnectionName;
                    if (!string.IsNullOrWhiteSpace(previousConnection) && this.appSettings.ConnectionList.Count > 0)
                    {
                        var item =
                            this.appSettings.ConnectionList.FirstOrDefault(c => c.DisplayName == previousConnection);
                        if (item != null)
                        {
                            isConnecting = true;
                            await this.Connect(item);
                            await Task.Delay(30000);
                            isConnecting = false;
                        }
                    }
                }
                else
                {
                    await Task.Delay(5000);
                }
            }
        }

        public async Task SendResult(ResultMessage resultMessage, string key = null)
        {
            key = key ?? resultMessage.Type.ToString();
            var priority = this.GetPriorityOnKey(key);
            var json = JsonConvert.SerializeObject(
                resultMessage, 
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            await this.service.SendMessage(json, key, priority);
            this.Log("S: " + json + "\r\n");
        }

        private int GetPriorityOnKey(string key)
        {
            return "AGMLQP".Contains(key) ? 20 : 10;
        }

        private async void Sensors_OnSensorUpdated(XYZ data)
        {
            var builder = new StringBuilder();
            builder.Append("{\"Service\":\"SENSOR\", \"Type\":\"" + data.Tag + "\",\"Id\":");
            builder.AppendFormat("{0}", data.Id);
            foreach (var t in data.Data)
            {
                builder.AppendFormat(",\"{0}\":{1:0.0###}", t.Name, t.Value);
            }

            builder.Append("}");

            await this.service.SendMessage(builder.ToString(), data.Tag, 20);
        }

        private void InitializeManager()
        {
            this.manager = new Manager();
            this.manager.StringReceived += this.StringReceived;
            this.manager.MessageReceived += this.QueueMessage;
        }

        private async Task InitializeCamera()
        {
            try
            {
                if (!this.isCameraInitialized && !this.isCameraInitializing)
                {
                    await this.dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, 
                        async () =>
                            {
                                await this.camera.InitializePreview(this.canvasBackground);
                                this.isCameraInitialized = true;
                                this.isCameraInitializing = false;
                            });
                }
            }
            catch (Exception)
            {
                // camera not available
                this.isCameraInitialized = false;
                this.isCameraInitializing = false;
            }
        }

        private async void StringReceived(string message)
        {
            this.recentStringReceivedTick = DateTime.UtcNow.Ticks;

            if (message.Equals("{}"))
            {
                // indicates ready for messages
                this.service.IsClearToSend = true;
                var ticks = DateTime.UtcNow.Ticks;

                if (ticks > this.nextPingCheck)
                {
                    this.nextPingCheck = long.MaxValue - pingCheck < ticks ? 0 : ticks + pingCheck;
                    this.sentPingTick = DateTime.UtcNow.Ticks;
                    await this.SendResult(new SystemResultMessage("PING"));
                }
            }
        }

        private async void PopulateList()
        {
            Connections list;

            if (this.service == null)
            {
                return;
            }

            try
            {
                list = await this.service.GetConnections();
                if (list == null || list.Count == 0)
                {
                    this.appSettings.ConnectionList.Clear();
                    return;
                }
            }
            catch (Exception ex)
            {
                App.Telemetry.TrackException(ex);
                return;
            }

            if (!list.Any())
            {
                await
                    this.dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, 
                        () => { App.Telemetry.TrackEvent("VirtualShieldConnectionEnumerationFail"); });

                return;
            }

            var connections = new Connections();

            foreach (var item in list)
            {
                connections.Add(item);
            }

            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this.appSettings.ConnectionList = connections;
                        App.Telemetry.TrackEvent("VirtualShieldConnectionEnumerationSuccess");
                    });
        }

        public async void Disconnect()
        {
            if (this.currentConnection != null)
            {
                await this.dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, 
                    () =>
                        {
                            this.appSettings.CurrentConnectionState = (int)ConnectionState.Disconnecting;
                            App.Telemetry.Context.Properties["connection.state"] =
                                ConnectionState.Disconnecting.ToString();
                        });

                this.service.Disconnect(this.currentConnection);
                this.currentConnection = null;

                await this.dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, 
                    () =>
                        {
                            this.appSettings.CurrentConnectionState = (int)ConnectionState.NotConnected;
                            App.Telemetry.Context.Properties["connection.state"] =
                                ConnectionState.NotConnected.ToString();
                        });

                App.Telemetry.TrackEvent("Disconnect");
            }
        }

        public async Task<bool> Connect(Connection selectedConnection)
        {
            var result = false;
            if (this.currentConnection != null)
            {
                this.service.Disconnect(this.currentConnection);
                await Task.Delay(1000);
            }

            try
            {
                var worked = false;
                this.connectionStopwatch.Reset();
                this.connectionStopwatch.Start();

                await this.dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, 
                    () =>
                        {
                            this.appSettings.CurrentConnectionState = (int)ConnectionState.Connecting;
                            App.Telemetry.Context.Properties["connection.state"] = ConnectionState.Connecting.ToString();
                            App.Telemetry.Context.Properties["connection.name"] = string.Format(
                                "{0:X}", 
                                selectedConnection.DisplayName.GetHashCode());
                            App.Telemetry.Context.Properties["connection.detail"] = string.Format(
                                "{0:X}", 
                                GetConnectionDetail(selectedConnection).GetHashCode());
                        });

                await this.dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, 
                    async () =>
                        {
                            App.Telemetry.TrackEvent("Connection_Attempt");
                            worked = await this.service.Connect(selectedConnection);
                            this.connectionStopwatch.Stop();

                            if (!worked)
                            {
                                this.appSettings.CurrentConnectionState = (int)ConnectionState.CouldNotConnect;
                                App.Telemetry.Context.Properties["connection.state"] = "Failed";
                            }
                            else
                            {
                                this.appSettings.CurrentConnectionState = (int)ConnectionState.Connected;
                                this.currentConnection = selectedConnection;
                                this.appSettings.PreviousConnectionName = this.currentConnection.DisplayName;
                                App.Telemetry.Context.Properties["connection.state"] =
                                    ConnectionState.Connected.ToString();
                                result = true;
                            }

                            App.Telemetry.TrackEvent("Connection");
                        });

                if (this.service.CharEventHandlerCount == 0)
                {
                    this.service.CharReceived += c => this.manager.OnCharsReceived(c.ToString());
                    this.service.CharEventHandlerCount++;
                }
            }
            catch (Exception e)
            {
                this.Log("!:error connecting:" + e.Message);

                await
                    this.dispatcher.RunAsync(
                        CoreDispatcherPriority.Normal, 
                        () => { this.appSettings.CurrentConnectionState = (int)ConnectionState.CouldNotConnect; });
                App.Telemetry.TrackException(e);
            }

            return result;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.InitializeManager();

            if ((!this.appSettings.AutoConnect || string.IsNullOrWhiteSpace(this.appSettings.PreviousConnectionName))
                && this.currentConnection == null)
            {
                this.Frame.Navigate(typeof(SettingsPage));
            }

            if (!string.IsNullOrWhiteSpace(e.Parameter?.ToString()))
            {
                this.OnLaunchWhileActive(e.Parameter.ToString());
            }
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            await
                this.SendResult(
                    new ScreenResultMessage
                        {
                            Service = "LCDG", 
                            Type = 'S', 
                            Action = "clicked", 
                            Result = (sender as Button)?.Tag.ToString(), 
                            Area = "CONTROL"
                        });
        }

        private async void UIElement_OnHolding(object sender, HoldingRoutedEventArgs e)
        {
            await
                this.SendResult(
                    new ScreenResultMessage
                        {
                            Service = "LCDG", 
                            Type = 'S', 
                            Action = "holding", 
                            Result = (sender as Button)?.Tag.ToString(), 
                            Area = "CONTROL"
                        });
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var swi = sender as ToggleSwitch;

            this.manager.OnStringReceived(
                "{ 'Service': 'SENSORS', 'Sensors': [ {'" + swi.Tag + "' : " + (swi.IsOn ? "true" : "false") + "} ] }");
        }

        public void RefreshConnections()
        {
            this.PopulateList();
        }

        private void AppButtons_OnClick(object sender, RoutedEventArgs e)
        {
            var appbutton = sender as AppBarButton;

            if (appbutton.Tag.Equals("Settings"))
            {
                this.Frame.Navigate(typeof(SettingsPage));
            }
            else if (appbutton.Tag.Equals("Control"))
            {
                this.appSettings.IsControlscreen = true;
            }
            else if (appbutton.Tag.Equals("Display"))
            {
                // to full screen
                this.appSettings.IsFullscreen = true;
            }
            else if (appbutton.Tag.Equals("Refresh"))
            {
                this.SendResult(new SystemResultMessage("REFRESH"), "!R").Wait();
            }
            else if (appbutton.Tag.Equals("CloseApp"))
            {
                this.CheckAlwaysRunning(false);

                this.isRunning = false;
                if (this.currentConnection != null)
                {
                    this.service.Disconnect(this.currentConnection);
                }

                Application.Current.Exit();
            }

            this.commandBar.IsOpen = false;
        }

        public void UpdateDestinations()
        {
            // As of right now we only support one destination
            this.destinations.Clear();

            // Add the AzureBlob Destination Endpoint
            var blobDestination = new AzureBlob(this.appSettings.BlobAccountName, this.appSettings.BlobAccountKey);
            this.destinations.Add(blobDestination);
        }

        public async void OnLaunchWhileActive(string arguments)
        {
            try
            {
                var notify = JsonConvert.DeserializeObject<NotificationLaunch>(arguments);
                if (notify != null)
                {
                    await
                        this.dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal, 
                            async () =>
                                {
                                    await
                                        this.SendResult(
                                            new NotifyResultMessage
                                                {
                                                    Id = int.Parse(notify.Id), 
                                                    Service = "NOTIFY", 
                                                    Type = 'N', 
                                                    Tag = notify.Tag
                                                });
                                });
                }
            }
            catch (Exception e)
            {
                this.Log("Toast:" + e.Message);
            }
        }

        private static string GetConnectionDetail(Connection connection)
        {
            if (null == connection.Source)
            {
                return string.Empty;
            }

            var deviceInformation = connection.Source as DeviceInformation;
            if ( deviceInformation != null )
            {
                return deviceInformation.Id;
            }

            var endpointPair = connection.Source as EndpointPair;
            if ( endpointPair != null )
            {
                return string.Format("{0}:{1}", endpointPair.RemoteHostName, endpointPair.RemoteServiceName);
            }

            return connection.Source.ToString();
        }
    }

    public class NotificationLaunch
    {
        public string Type { get; set; }

        public string Id { get; set; }

        public string Tag { get; set; }
    }
}