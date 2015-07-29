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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Shield.Communication;
using Shield.Communication.Destinations;
using Shield.Communication.Services;
using Shield.Core;
using Shield.Core.Models;
using Shield.Services;
using Shield.ViewModels;
using Windows.UI.ViewManagement;
using Microsoft.ApplicationInsights;

namespace Shield
{
    public sealed partial class MainPage : Page
    {
        private const long pingCheck = 60*10000000; //1min
        internal readonly CoreDispatcher dispatcher;
        private readonly MainViewModel model;
        internal readonly Dictionary<string, int> sensors = new Dictionary<string, int>();
        private Audio audio;
        private Camera camera;
        public Connection currentConnection;
        private List<IDestination> destinations;
        private bool isCameraInitialized;
        private DisplayRequest keepScreenOnRequest;
        private Manager manager;
        private long nextPingCheck = DateTime.UtcNow.Ticks;
        private long recentStringReceivedTick;
        // Setup for ServerClient Connection
        public string remoteHost = "SERVER_IP_OR_ADDRESS";
        public string remoteService = "SERVER_PORT";
        private long sentPingTick;
        public ServiceBase service;
        public bool IsInSettings = false;

        private AppSettings appSettings = null;

        public static MainPage Instance = null;
        private TelemetryClient telemetry = new TelemetryClient();

        private Screen screen;
        private Web web;

        public MainPage()
        {
            Instance = this;

            appSettings = (AppSettings)App.Current.Resources["appSettings"];

            InitializeComponent();
            model = new MainViewModel();
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            Initialize();

            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.ViewManagement.StatusBar"))
            {
                if (StatusBar.GetForCurrentView() != null)
                {
                    StatusBar.GetForCurrentView().BackgroundOpacity = 0.50;
                }
            }
        }

        public Sensors Sensors { get; set; }

        private bool isRunning = false;

        private Dictionary<string, ServiceBase> services = new Dictionary<string, ServiceBase>();

        public void SetService()
        {
            if (appSettings.ConnectionIndex < 0)
            {
                return;
            }

            ServiceBase.OnConnectHandler OnConnection = async connection =>
            {
                await SendResult(new SystemResultMessage("CONNECT"), "!!");
            };

            ServiceBase.OnConnectHandler OnDisconnected = connection =>
            {
                this.Disconnect();
            };

            if (service != null)
            {
                service.OnConnect -= OnConnection;
                service.OnDisconnected -= OnDisconnected;
            }

            if (appSettings.ConnectionIndex == 0)
            {
                service = services.ContainsKey("Bluetooth") ? services["Bluetooth"] : new Bluetooth();
                services["Bluetooth"] = service;
            }
            else
            {
                service = services.ContainsKey("Wifi") ? services["Wifi"] : new Wifi();
                services["Wifi"] = service;

                if (appSettings.ConnectionIndex == 2 && !string.IsNullOrWhiteSpace(appSettings.Hostname))
                {
                    service.SetClient("added",
                        new Connection("added",
                            new RemotePeer(null, new HostName(appSettings.Hostname), "1235")));
                }
            }

            service.OnConnect += OnConnection;
            service.OnDisconnected += OnDisconnected;

            service.Initialize(!appSettings.MissingBackButton);

            service.ListenForBeacons();
            RefreshConnections();
        }

        private void Initialize()
        {
            destinations = new List<IDestination>();

            DataContext = model;
            model.Sensors = new Sensors(dispatcher);

            MessageFactory.LoadClasses();

            Sensors = model.Sensors;

            // the Azure BLOB storage helper is handed to both Camera and audio for stream options
            // if it is not used, no upload will to Azure BLOB will be done
            camera = new Camera();
            audio = new Audio();

            Sensors.OnSensorUpdated += Sensors_OnSensorUpdated;
            Sensors.Start();

            NavigationCacheMode = NavigationCacheMode.Required;

            canvas.PointerPressed += async (s, a) => await SendEvent(s, a, "pressed");
            canvas.PointerReleased += async (s, a) => await SendEvent(s, a, "released");

            Display.PointerPressed += async (s, a) => await SendEvent(s, a, "pressed");
            Display.PointerReleased += async (s, a) => await SendEvent(s, a, "released");

            screen = new Screen(this);
            web = new Web(this);
            CheckAlwaysRunning();

            isRunning = true;
#pragma warning disable 4014
            Task.Run(() =>
            {
                SetService();
                AutoReconnect();
            });
            Task.Run(() => { ProcessMessages(); });
#pragma warning restore 4014
        }


        public void CheckAlwaysRunning(bool? force = null)
        {
            if ((force.HasValue && force.Value) || appSettings.AlwaysRunning)
            {
                keepScreenOnRequest = new DisplayRequest();
                keepScreenOnRequest.RequestActive();
            }
            else if (keepScreenOnRequest != null)
            {
                keepScreenOnRequest.RequestRelease();
                keepScreenOnRequest = null;
            }
        }

        private async void AutoReconnect()
        {
            var isConnecting = false;
            while (isRunning)
            {
                if (!isConnecting && this.currentConnection == null && !IsInSettings)
                {
                    var previousConnection = appSettings.PreviousConnectionName;
                    if (!string.IsNullOrWhiteSpace(previousConnection) && appSettings.ConnectionList.Count > 0)
                    {
                        var item = appSettings.ConnectionList.FirstOrDefault(c => c.DisplayName == previousConnection);
                        if (item != null)
                        {
                            isConnecting = true;
                            await Connect(item);
                            await Task.Delay(30000);
                            isConnecting = false;
                        }
                    }
                }
            }
        }

        public async Task SendResult(ResultMessage resultMessage, string key = null)
        {
            key = key ?? resultMessage.Type.ToString();
            var priority = GetPriorityOnKey(key);
            var json = JsonConvert.SerializeObject(resultMessage,
                new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
            await service.SendMessage(json, key, priority);
            Log("S: " + json + "\r\n");
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

            await service.SendMessage(builder.ToString(), data.Tag, 20);
        }

        private void InitializeManager()
        {
            manager = new Manager();
            manager.StringReceived += StringReceived;
            manager.MessageReceived += QueueMessage;
        }

        private async Task InitializeCamera()
        {
            try
            {
                if (!isCameraInitialized && !isCameraInitializing)
                {
                    await
                        dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            async () =>
                            {
                                await camera.InitializePreview(canvasBackground);
                                isCameraInitialized = true;
                                isCameraInitializing = false;
                            });
                }
            }
            catch (Exception)
            {
                //camera not available
                isCameraInitialized = false;
                isCameraInitializing = false;
            }
        }

        private async void StringReceived(string message)
        {
            recentStringReceivedTick = DateTime.UtcNow.Ticks;

            if (message.Equals("{}"))
            {
                //indicates ready for messages
                service.IsClearToSend = true;
                var ticks = DateTime.UtcNow.Ticks;

                if (ticks > nextPingCheck)
                {
                    nextPingCheck = long.MaxValue - pingCheck < ticks ? 0 : ticks + pingCheck;
                    sentPingTick = DateTime.UtcNow.Ticks;
                    await SendResult(new SystemResultMessage("PING"));
                }
            }
        }

        private async void PopulateList()
        {
            Connections list;

            if (service == null)
            {
                return;
            }

            try
            {
                list = await service.GetConnections();
                if (list == null || list.Count == 0)
                {
                    appSettings.ConnectionList.Clear();
                    return;
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return;
            }

            if (!list.Any())
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    telemetry.TrackEvent("VirtualShieldConnectionEnumerationFail");
                });

                return;
            }

            var connections = new Connections();

            foreach (var item in list)
            {
                connections.Add(item);
            }

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                appSettings.ConnectionList = connections;
                telemetry.TrackEvent("VirtualShieldConnectionEnumerationSuccess");
            });
        }

        public async void Disconnect()
        {
            if (currentConnection != null)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    appSettings.CurrentConnectionState = (int)ConnectionState.Disconnecting;
                });
                
                service.Disconnect(currentConnection);
                currentConnection = null;

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    appSettings.CurrentConnectionState = (int)ConnectionState.NotConnected;
                });

                telemetry.TrackEvent("VirtualShieldDisconnect");
            }
        }

        public async Task<bool> Connect(Connection selectedConnection)
        {
            bool result = false;
            if (currentConnection != null)
            {
                service.Disconnect(currentConnection);
                await Task.Delay(1000);
            }

            try
            {
                bool worked = false;
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    appSettings.CurrentConnectionState = (int)ConnectionState.Connecting;
                });

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    worked = await service.Connect(selectedConnection);

                    if (!worked)
                    {
                        appSettings.CurrentConnectionState = (int)ConnectionState.CouldNotConnect;
                        telemetry.TrackEvent("VirtualShieldConnectionFail");
                    }
                    else
                    {
                        appSettings.CurrentConnectionState = (int)ConnectionState.Connected;
                        currentConnection = selectedConnection;
                        appSettings.PreviousConnectionName = currentConnection.DisplayName;
                        telemetry.TrackEvent("VirtualShieldConnectionSuccess");
                        result = true;
                    }
                });

                if (service.CharEventHandlerCount == 0)
                {
                    service.CharReceived += c => manager.OnCharsReceived(c.ToString());
                    service.CharEventHandlerCount++;
                }
            }
            catch (Exception e)
            {
                this.Log("!:error connecting:" + e.Message);

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    appSettings.CurrentConnectionState = (int)ConnectionState.CouldNotConnect;
                });
                telemetry.TrackException(e);
            }

            return result;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            InitializeManager();

            if ((!appSettings.AutoConnect || string.IsNullOrWhiteSpace(appSettings.PreviousConnectionName))
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
            await SendResult(new ScreenResultMessage
            {
                Service = "LCDG",
                Type = 'S',
                Action = "clicked",
                Result = ((sender as Button).Tag).ToString(),
                Area = "CONTROL"
            });
        }

        private async void UIElement_OnHolding(object sender, HoldingRoutedEventArgs e)
        {
            await SendResult(new ScreenResultMessage
            {
                Service = "LCDG",
                Type = 'S',
                Action = "holding",
                Result = ((sender as Button).Tag).ToString(),
                Area = "CONTROL"
            });
        }


        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var swi = sender as ToggleSwitch;

            manager.OnStringReceived("{ 'Service': 'SENSORS', 'Sensors': [ {'" + swi.Tag + "' : " +
                                     (swi.IsOn ? "true" : "false") + "} ] }");
        }

        public void RefreshConnections()
        {
            PopulateList();
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
                appSettings.IsControlscreen = true;
            }
            else if (appbutton.Tag.Equals("Display")) // to full screen
            {
                appSettings.IsFullscreen = true;
            }
            else if (appbutton.Tag.Equals("Refresh"))
            {
                SendResult(new SystemResultMessage("REFRESH"), "!R").Wait();
            }
            else if (appbutton.Tag.Equals("CloseApp"))
            {
                CheckAlwaysRunning(false);

                isRunning = false;
                if (this.currentConnection != null)
                {
                    this.service.Disconnect(this.currentConnection);
                }

                Application.Current.Exit();
            }

            commandBar.IsOpen = false;
        }

        public void UpdateDestinations()
        {
            // As of right now we only support one destination
            destinations.Clear();

            // Add the AzureBlob Destination Endpoint
            var blobDestination = new AzureBlob(appSettings.BlobAccountName, appSettings.BlobAccountKey);
            destinations.Add(blobDestination);
        }

        public async void OnLaunchWhileActive(string arguments)
        {
            try
            {
                var notify = JsonConvert.DeserializeObject<NotificationLaunch>(arguments);
                if (notify != null)
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        async () =>
                        {
                            await
                                SendResult(new NotifyResultMessage()
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
    }

    public class NotificationLaunch
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public string Tag { get; set; }
    }
}