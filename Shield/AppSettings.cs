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

using Shield.Communication;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shield
{
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
        private Windows.Storage.ApplicationDataContainer localSettings;
        private Connections connectionList;

        public event PropertyChangedEventHandler PropertyChanged;
        private string[] ConnectionStateText;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static AppSettings Instance;
        private bool isLogging;
        private StringBuilder log = new StringBuilder();

        private bool isListening = false;

        public AppSettings()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            ConnectionStateText = new[]
            {
                loader.GetString("NotConnected"), loader.GetString("Connecting"), loader.GetString("Connected"),
                loader.GetString("CouldNotConnect"), loader.GetString("Disconnecting")
            };
            DeviceNames = new List<string>
            {
                loader.GetString("Bluetooth"),
                loader.GetString("NetworkDiscovery"),
                loader.GetString("NetworkDirect")
            };

            try
            {
                localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                connectionList = new Connections();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception while using LocalSettings: " + e.ToString());
                throw;
            }
        }

        public bool AddOrUpdateValue(Object value, [CallerMemberName] string Key = null)
        {
            bool valueChanged = false;

            if (localSettings.Values.ContainsKey(Key))
            {
                if (localSettings.Values[Key] != value)
                {
                    localSettings.Values[Key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                localSettings.Values.Add(Key, value);
                valueChanged = true;
            }

            return valueChanged;
        }

        public T GetValueOrDefault<T>(T defaultValue, [CallerMemberName] string Key = null)
        {
            T value;

            // If the key exists, retrieve the value.
            if (localSettings.Values.ContainsKey(Key))
            {
                value = (T) localSettings.Values[Key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        public bool Remove(Object value, [CallerMemberName] string Key = null)
        {
            if (localSettings.Values.ContainsKey(Key))
            {
                localSettings.DeleteContainer(Key);
                return true;
            }

            return false;
        }

        public bool AutoConnect
        {
            get { return GetValueOrDefault(true); }
            set { AddOrUpdateValue(value); }
        }

        public bool IsListening
        {
            get { return isListening; }
            set
            {
                isListening = value;
                OnPropertyChanged("IsListening");
            }
        }

        public bool AlwaysRunning
        {
            get { return GetValueOrDefault(true); }
            set { AddOrUpdateValue(value); }
        }

        public bool NoListVisible => !ListVisible;

        public bool ListVisible => ConnectionList != null && ConnectionList.Any();

        public int ConnectionIndex
        {
            get { return BluetoothVisible ? 0 : NetworkVisible ? 1 : NetworkDirectVisible ? 2 : -1; }
            set
            {
                BluetoothVisible = value == 0;
                NetworkVisible = value == 1;
                NetworkDirectVisible = value == 2;
            }
        }

        public bool NotNetworkDirectVisible => !NetworkDirectVisible;

        public bool BluetoothVisible
        {
            get { return GetValueOrDefault(true); }
            set
            {
                AddOrUpdateValue(value);
                if (value)
                {
                    NetworkVisible = false;
                    NetworkDirectVisible = false;
                }

                OnPropertyChanged("ConnectionIndex");
                OnPropertyChanged("NetworkDirectVisible");
                OnPropertyChanged("NotNetworkDirectVisible");
                MainPage.Instance.SetService();
            }
        }

        public bool NetworkVisible
        {
            get { return GetValueOrDefault(false); }
            set
            {
                AddOrUpdateValue(value);
                if (value)
                {
                    BluetoothVisible = false;
                    NetworkDirectVisible = false;
                }

                OnPropertyChanged("ConnectionIndex");
                OnPropertyChanged("NetworkDirectVisible");
                OnPropertyChanged("NotNetworkDirectVisible");
                MainPage.Instance.SetService();
            }
        }

        public bool NetworkDirectVisible
        {
            get { return GetValueOrDefault(false); }
            set
            {
                AddOrUpdateValue(value);
                if (value)
                {
                    BluetoothVisible = false;
                    NetworkVisible = false;
                }

                OnPropertyChanged("ConnectionIndex");
                OnPropertyChanged("NotNetworkDirectVisible");
                MainPage.Instance.SetService();
            }
        }
    

        public string Hostname
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }

        public string UserInfo1
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }

        public string UserInfo2
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }
        public string UserInfo3
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }

        public string UserInfo4
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }

        public int Hostport
        {
            get { return GetValueOrDefault(0); }
            set { AddOrUpdateValue(value); }
        }

        public string PreviousConnectionName
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }

        public string BlobAccountName
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }
        public string BlobAccountKey
        {
            get { return GetValueOrDefault(""); }
            set { AddOrUpdateValue(value); }
        }

        public Connections ConnectionList
        {
            get { return connectionList; }
            set
            {
                connectionList = value;
                OnPropertyChanged("ConnectionList");
                OnPropertyChanged("ListVisible");
                OnPropertyChanged("NoListVisible");
            }
        }

        public bool IsFullscreen
        {
            get { return GetValueOrDefault(true); }
            set {
                AddOrUpdateValue(value);
                OnPropertyChanged("IsFullscreen");
                OnPropertyChanged("IsControlscreen");
            }
        }

        public bool IsControlscreen
        {
            get { return !IsFullscreen; }
            set
            {
                IsFullscreen = !value;
            }
        }

        public bool IsLogging
        {
            get { return isLogging; }
            set
            {
                isLogging = value;
                OnPropertyChanged("IsLoggingSwitchText");
            }
        }

        public string IsLoggingSwitchText
        {
            get { return isLogging ? "Turn OFF" : "Turn ON"; }
        }

        public string LogText
        {
            get
            {
                return log.ToString();
            }
            set
            {
                log.Append(value);
                OnPropertyChanged("LogText");
            }
        }

        public StringBuilder Log
        {
            get { return log; }
        }

        public int CurrentConnectionState
        {
            get { return GetValueOrDefault((int) ConnectionState.NotConnected); }
            set {
                AddOrUpdateValue(value);
                OnPropertyChanged("CurrentConnectionStateText");
            }
        }

        public string CurrentConnectionStateText
        {
            get { return this.ConnectionStateText[CurrentConnectionState]; }
        }

        public List<string> DeviceNames { get; set; }

        public void ReportChanged(string key)
        {
            OnPropertyChanged(key);
        }

        public bool MissingBackButton => !Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
    }
}
