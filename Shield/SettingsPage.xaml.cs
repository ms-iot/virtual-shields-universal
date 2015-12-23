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
    using System.Text;
    using System.Threading.Tasks;

    using Shield.Communication;

    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class SettingsPage : Page
    {
        private readonly AppSettings appSettings;

        private readonly MainPage main;

        public SettingsPage()
        {
            this.main = MainPage.Instance;
            this.appSettings = (AppSettings)Application.Current.Resources["appSettings"];
            var index = this.appSettings.ConnectionIndex;

            this.InitializeComponent();

            this.ConnectSelection.SelectedIndex = index;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.main.IsInSettings = true;
            this.main.RefreshConnections();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            this.main.IsInSettings = false;
            base.OnNavigatingFrom(e);
        }

        private async void Reconnect_Click(object sender, RoutedEventArgs e)
        {
            if (this.connectList.SelectedItem != null)
            {
                var selectedConnection = this.connectList.SelectedItem as Connection;
                var result = await this.main.Connect(selectedConnection);
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
                            // ignore no back
                        }
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.main.RefreshConnections();
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            this.appSettings.PreviousConnectionName = string.Empty;

            if (this.main.currentConnection != null)
            {
                this.main.Disconnect();
            }
        }

        private void Log_Click(object sender, RoutedEventArgs e)
        {
            this.appSettings.IsLogging = !this.appSettings.IsLogging;
            if (this.appSettings.IsLogging)
            {
                this.main.logger = new StringBuilder();
            }
            else
            {
                this.appSettings.Log.Clear();
                this.appSettings.LogText = this.main.logger.ToString();
            }
        }

        private void Log_Clear(object sender, RoutedEventArgs e)
        {
            this.appSettings.Log.Clear();
            this.appSettings.LogText = string.Empty;
        }

        private void AlwaysRunning_Toggled(object sender, RoutedEventArgs e)
        {
            this.main.CheckAlwaysRunning();
        }

        private void UpdateDestinations(object sender, RoutedEventArgs e)
        {
            this.main.UpdateDestinations();
        }

        private void NavBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.appSettings.ConnectionIndex = ((ComboBox)sender).SelectedIndex;
        }
    }
}