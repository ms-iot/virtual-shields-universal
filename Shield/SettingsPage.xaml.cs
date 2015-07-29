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
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Shield
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private MainPage main;
        private AppSettings appSettings;
    
        public SettingsPage()
        {
            main = MainPage.Instance;
            appSettings = (AppSettings)App.Current.Resources["appSettings"];
            var index = appSettings.ConnectionIndex;

            this.InitializeComponent();

            Task.Delay(500).ContinueWith(async t =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, (() =>
                {
                    appSettings.ConnectionIndex = Math.Max(0, index);
                    if (appSettings.BluetoothVisible)
                    {
                        main.RefreshConnections();
                    }
                }));
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            main.IsInSettings = true;
            main.RefreshConnections();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            main.IsInSettings = false;
            base.OnNavigatingFrom(e);
        }

        private async void Reconnect_Click(object sender, RoutedEventArgs e)
        {
            if (connectList.SelectedItem != null)
            {
                var selectedConnection = connectList.SelectedItem as Connection;
                var result = await main.Connect(selectedConnection);
                if (result)
                {
                    await Task.Delay(2000);
                    if (this.Frame.CanGoBack)
                    {
                        try
                        {
                            this.Frame.GoBack();
                        }
                        catch (Exception)
                        {
                            //ignore no back
                        }
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            main.RefreshConnections();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            appSettings.PreviousConnectionName = string.Empty;

            if (main.currentConnection != null)
            {
                main.Disconnect();
            }
        }

        private void Log_Click(object sender, RoutedEventArgs e)
        {
            appSettings.IsLogging = !appSettings.IsLogging;
            if (appSettings.IsLogging)
            {
                main.logger = new StringBuilder();
            }
            else
            {
                appSettings.Log.Clear();
                appSettings.LogText = main.logger.ToString();
            }
        }
        private async void Log_Clear(object sender, RoutedEventArgs e)
        {
            appSettings.Log.Clear();
            appSettings.LogText = string.Empty;
        }

        private void AlwaysRunning_Toggled(object sender, RoutedEventArgs e)
        {
            main.CheckAlwaysRunning();
        }

        private void UpdateDestinations(object sender, RoutedEventArgs e)
        {
            main.UpdateDestinations();
        }

        private void NavBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }
    }
}
