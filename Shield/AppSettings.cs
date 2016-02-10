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
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;

    using Shield.Communication;

    using Windows.ApplicationModel.Resources;
    using Windows.Foundation.Metadata;
    using Windows.Storage;

    public enum ConnectionState
    {
        NotConnected = 0, 

        Connecting = 1, 

        Connected = 2, 

        CouldNotConnect = 3, 

        Disconnecting = 4
    }

    public class AppSettings : INotifyPropertyChanged
    {
        internal const int CONNECTION_BLUETOOTH = 0;

        internal const int CONNECTION_WIFI = 1;

        internal const int CONNECTION_MANUAL = 2;

        internal const int CONNECTION_USB = 3;

        public static AppSettings Instance;

        internal static readonly int BroadcastPort = 1235;

        private readonly string[] ConnectionStateText;

        private readonly ApplicationDataContainer localSettings;

        private Connections connectionList;

        private bool isListening;

        private bool isLogging;

        public AppSettings()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            var loader = new ResourceLoader();
            this.ConnectionStateText = new[]
                                           {
                                               loader.GetString("NotConnected"), loader.GetString("Connecting"), 
                                               loader.GetString("Connected"), loader.GetString("CouldNotConnect"), 
                                               loader.GetString("Disconnecting")
                                           };
            this.DeviceNames = new List<string>
                                   {
                                       loader.GetString("Bluetooth"), 
                                       loader.GetString("NetworkDiscovery"), 
                                       loader.GetString("NetworkDirect")
                                   };

            try
            {
                this.localSettings = ApplicationData.Current.LocalSettings;

                this.connectionList = new Connections();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception while using LocalSettings: " + e);
                throw;
            }
        }

        public bool AutoConnect
        {
            get
            {
                return this.GetValueOrDefault(true);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public bool IsListening
        {
            get
            {
                return this.isListening;
            }

            set
            {
                this.isListening = value;
                this.OnPropertyChanged("IsListening");
            }
        }

        public bool AlwaysRunning
        {
            get
            {
                return this.GetValueOrDefault(true);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public bool NoListVisible => !this.ListVisible;

        public bool ListVisible => this.ConnectionList != null && this.ConnectionList.Any();

        public int ConnectionIndex
        {
            get
            {
                return this.GetValueOrDefault(0);
            }

            set
            {
                this.AddOrUpdateValue(value);

                this.OnPropertyChanged("BluetoothVisible");
                this.OnPropertyChanged("NetworkDirectVisible");
                this.OnPropertyChanged("NotNetworkDirectVisible");

                MainPage.Instance.SetService();
            }
        }

        public string[] ConnectionItems => new[] { "Bluetooth", "Network", "Manual", "USB" };

        public bool NotNetworkDirectVisible => !this.NetworkDirectVisible;

        public bool BluetoothVisible => this.ConnectionIndex == 0;

        public bool NetworkVisible => this.ConnectionIndex == 1;

        public bool NetworkDirectVisible => this.ConnectionIndex == 2;

        public bool USBVisible => this.ConnectionIndex == 3;

        public string Hostname
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string UserInfo1
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string UserInfo2
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string UserInfo3
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string UserInfo4
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public int Hostport
        {
            get
            {
                return this.GetValueOrDefault(0);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string PreviousConnectionName
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string BlobAccountName
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public string BlobAccountKey
        {
            get
            {
                return this.GetValueOrDefault(string.Empty);
            }

            set
            {
                this.AddOrUpdateValue(value);
            }
        }

        public Connections ConnectionList
        {
            get
            {
                return this.connectionList;
            }

            set
            {
                this.connectionList = value;
                this.OnPropertyChanged("ConnectionList");
                this.OnPropertyChanged("ListVisible");
                this.OnPropertyChanged("NoListVisible");
            }
        }

        public bool IsFullscreen
        {
            get
            {
                return this.GetValueOrDefault(true);
            }

            set
            {
                this.AddOrUpdateValue(value);
                this.OnPropertyChanged("IsFullscreen");
                this.OnPropertyChanged("IsControlscreen");
            }
        }

        public bool IsControlscreen
        {
            get
            {
                return !this.IsFullscreen;
            }

            set
            {
                this.IsFullscreen = !value;
            }
        }

        public bool IsLogging
        {
            get
            {
                return this.isLogging;
            }

            set
            {
                this.isLogging = value;
                this.OnPropertyChanged("IsLoggingSwitchText");
            }
        }

        public string IsLoggingSwitchText
        {
            get
            {
                return this.isLogging ? "Turn OFF" : "Turn ON";
            }
        }

        public string LogText
        {
            get
            {
                return this.Log.ToString();
            }

            set
            {
                this.Log.Append(value);
                this.OnPropertyChanged("LogText");
            }
        }

        public StringBuilder Log { get; } = new StringBuilder();

        public int CurrentConnectionState
        {
            get
            {
                return this.GetValueOrDefault((int)ConnectionState.NotConnected);
            }

            set
            {
                this.AddOrUpdateValue(value);
                this.OnPropertyChanged("CurrentConnectionStateText");
            }
        }

        public string CurrentConnectionStateText
        {
            get
            {
                return this.ConnectionStateText[this.CurrentConnectionState];
            }
        }

        public List<string> DeviceNames { get; set; }

        public bool MissingBackButton => !ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = this.PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool AddOrUpdateValue(object value, [CallerMemberName] string Key = null)
        {
            var valueChanged = false;

            if (this.localSettings.Values.ContainsKey(Key))
            {
                if (this.localSettings.Values[Key] != value)
                {
                    this.localSettings.Values[Key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                this.localSettings.Values.Add(Key, value);
                valueChanged = true;
            }

            return valueChanged;
        }

        public T GetValueOrDefault<T>(T defaultValue, [CallerMemberName] string Key = null)
        {
            T value;

            // If the key exists, retrieve the value.
            if (this.localSettings.Values.ContainsKey(Key))
            {
                try
                {
                    value = (T)this.localSettings.Values[Key];
                }
                catch (InvalidCastException)
                {
                    value = defaultValue;
                }
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        public bool Remove(object value, [CallerMemberName] string Key = null)
        {
            if (this.localSettings.Values.ContainsKey(Key))
            {
                this.localSettings.DeleteContainer(Key);
                return true;
            }

            return false;
        }

        public void ReportChanged(string key)
        {
            this.OnPropertyChanged(key);
        }
    }
}