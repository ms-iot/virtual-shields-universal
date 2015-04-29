
using Shield.Communication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
        private AppSettings appSettings = null;
    
        public SettingsPage()
        {
            main = MainPage.Instance;
            appSettings = (AppSettings)App.Current.Resources["appSettings"];
            var index = appSettings.ConnectionIndex;

            this.InitializeComponent();

            appSettings.ConnectionIndex = Math.Max(0, index);
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
        }
        private void Log_Clear(object sender, RoutedEventArgs e)
        {
            appSettings.Log.Clear();
            appSettings.LogText = string.Empty;
        }

        private void AlwaysRunning_Toggled(object sender, RoutedEventArgs e)
        {
            main.CheckAlwaysRunning();
        }

        private void updateDestinations(object sender, RoutedEventArgs e)
        {
            main.UpdateDestinations();
        }

        private void NavBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }
    }
}
