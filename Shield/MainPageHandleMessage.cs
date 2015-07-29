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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Email;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
﻿using Windows.Graphics.Display;
﻿using Windows.Media.SpeechRecognition;
using Windows.Phone.Devices.Notification;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Shield.Core;
using Shield.Core.Models;
using Shield.Services;
using EmailMessage = Windows.ApplicationModel.Email.EmailMessage;

namespace Shield
{
    public sealed partial class MainPage : Page
    {
        private readonly bool allowGlobalCommandBlocking = false;

        private readonly Dictionary<string, Queue<MessageBase>> keyedMessageQueues =
            new Dictionary<string, Queue<MessageBase>>();

        private readonly Queue<string> keys = new Queue<string>();
        private readonly Dictionary<string, bool> keysInProcess = new Dictionary<string, bool>();
        private bool isCameraInitializing;

        public Dictionary<string, Dictionary<string, string>> mainDictionary =
            new Dictionary<string, Dictionary<string, string>>();

        private Speech speechService;
        public StringBuilder logger;

        private void QueueMessage(MessageBase message)
        {
            lock (keyedMessageQueues)
            {
                if (!keyedMessageQueues.ContainsKey(message.Service))
                {
                    keyedMessageQueues.Add(message.Service, new Queue<MessageBase>());
                }

                keyedMessageQueues[message.Service].Enqueue(message);
            }

            lock (keys)
            {
                keys.Enqueue(message.Service);
            }
        }

        private async void ProcessMessages()
        {
            var localKeysInProcess = new Dictionary<string, bool>();

            while (isRunning)
            {
                int count;
                lock (keys)
                {
                    count = keys.Count;
                }

                if (count <= 0)
                {
                    await Task.Delay(50);
                    continue;
                }

                string service;
                lock (keys)
                {
                    service = keys.Dequeue();
                }

                var inUse = allowGlobalCommandBlocking && localKeysInProcess.ContainsKey(service) &&
                            localKeysInProcess[service];

                if (!inUse)
                {
                    lock (keysInProcess)
                    {
                        inUse = keysInProcess.ContainsKey(service) && keysInProcess[service];
                    }
                }

                if (inUse)
                {
                    //in use - back of the line
                    lock (keys)
                    {
                        keys.Enqueue(service);
                    }

                    await Task.Delay(50);
                }
                else
                {
                    localKeysInProcess[service] = true;

                    MessageBase message;
                    lock (keyedMessageQueues)
                    {
                        message = keyedMessageQueues[service].Dequeue();
                    }

                    try
                    {
                        await ProcessMessage(message);
                    }
                    finally
                    {
                        localKeysInProcess[service] = false;
                    }
                }
            }
        }

        private async Task ProcessMessage(MessageBase message)
        {
            try
            {
                await HandleMessageUnprotected(message);
            }
            catch (UnsupportedSensorException e)
            {
                await SendResult(new ResultMessage(message) {ResultId = -2, Result = e.Message});
            }
            catch (Exception e)
            {
                await SendResult(new ResultMessage(message) {ResultId = -1, Result = e.Message});
            }
        }

        private async Task HandleMessageUnprotected(MessageBase message)
        {
            Log("R: " + message._Source + "\r\n");

            try
            {
                if (message.Service != "SYSTEM")
                {
                    var dictionary = new Dictionary<string, string> {{"Type", message.Service}};
                    telemetry.TrackEvent("MessageInfo", dictionary);
                }
            }
            catch (Exception)
            {
                //ignore telemetry errors if any

            }

            switch (message.Service)
            {
                case "SYSTEM":
                {
                    if (message.Action.Equals("PONG"))
                    {
                        var totalRoundTrip = recentStringReceivedTick - sentPingTick;
                        telemetry.TrackMetric("VirtualShieldPingPongTimeDifferenceMillisec",
                            totalRoundTrip/TimeSpan.TicksPerMillisecond);
                    }
                    else if (message.Action.Equals("START"))
                    {
                        //reset orientation
                        DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;

                        //turn off all sensors, accept buffer length
                        var switches = new SensorSwitches {A = 0, G = 0, L = 0, M = 0, P = 0, Q = 0};
                        var sensors = new List<SensorSwitches>();
                        sensors.Add(switches);

                        ToggleSensors(new SensorMessage {Sensors = sensors, Id = 0});
                    }

                    break;
                }
                case "SMS":
                {
                    var smsService = new Sms();
                    var sms = message as SmsMessage;

                    StorageFile attachment = null;
                    if (!string.IsNullOrWhiteSpace(sms.Attachment))
                    {
                        attachment = await StorageFile.GetFileFromPathAsync(sms.Attachment);
                    }

                    smsService.Send(sms.To, sms.Message, attachment, null);
                    break;
                }

                case "EMAIL":
                {
                    var email = new EmailMessage();
                    var emailMessage = message as Core.Models.EmailMessage;
                    email.Body = emailMessage.Message;
                    email.Subject = emailMessage.Subject;
                    email.To.Add(new EmailRecipient(emailMessage.To));
                    if (!string.IsNullOrWhiteSpace(emailMessage.Cc))
                    {
                        email.CC.Add(new EmailRecipient(emailMessage.To));
                    }

                    if (!string.IsNullOrWhiteSpace(emailMessage.Attachment))
                    {
                        var storageFile = await StorageFile.GetFileFromPathAsync(emailMessage.Attachment);
                        var stream = RandomAccessStreamReference.CreateFromFile(storageFile);

                        var attachment = new EmailAttachment(
                            storageFile.Name,
                            stream);

                        email.Attachments.Add(attachment);
                    }

                    await EmailManager.ShowComposeNewEmailAsync(email);

                    break;
                }

                case "NOTIFY":
                {
                    var notify = message as NotifyMessage;
                    if (string.IsNullOrWhiteSpace(notify.Action))
                    {
                        return;
                    }

                    if (notify.Action.ToUpperInvariant().Equals("TOAST"))
                    {
                        await SendToastNotification(notify);
                    }
                    else if (notify.Action.ToUpperInvariant().Equals("TILE"))
                    {
                        await SendTileNotification(notify);
                    }

                    break;
                }

                case "LCDG":
                case "LCDT":
                {
                    await
                        dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            () => { screen.LcdPrint(message as ScreenMessage); });
                    break;
                }

                case "LOG":
                {
                    await screen.LogPrint(message as ScreenMessage);
                    break;
                }

                case "SPEECH":
                {
                    Speak(message as SpeechMessage);
                    break;
                }

                case "RECOGNIZE":
                {
                    Recognize(message as SpeechRecognitionMessage);
                    break;
                }

                case "SENSORS":
                {
                    ToggleSensors(message as SensorMessage);
                    break;
                }

                case "WEB":
                {
                    await web.RequestUrl(message as WebMessage);
                    break;
                }

                case "CAMERA":
                {
                    var camMsg = message as CameraMessage;
                    if (camMsg.Action != null && camMsg.Message != null && camMsg.Message.Equals("PREVIEW"))
                    {
                        if (camMsg.Action.Equals("ENABLE") && !isCameraInitialized)
                        {
                            await InitializeCamera();
                            camera.isPreviewing = true;
                        }
                        else if (camMsg.Action.Equals("DISABLE"))
                        {
                            camera.isPreviewing = false;
                        }
                    }
                    else
                    {
                        await TakePicture(message as CameraMessage);
                    }

                    break;
                }

                case "VIBRATE":
                {
                    Vibrate(message as TimingMessage);
                    break;
                }

                case "MICROPHONE":
                {
                    await Record(message as TimingMessage);
                    break;
                }

                case "PLAY":
                {
                    await Play(message as TimingMessage);
                    break;
                }

                case "DEVICE":
                {
                    await DeviceInfo(message as DeviceMessage);
                    break;
                }
            }
        }

        private async Task DeviceInfo(DeviceMessage devMessage)
        {
            var action = devMessage.Action.ToUpperInvariant();

            switch (action)
            {
                case "CAPABILITIES":
                {
                    var resultId = 0;
                    var result = new StringBuilder();

                    if (Accelerometer.GetDefault() != null)
                    {
                        resultId += 1;
                        result.Append("A");
                    }

                    if (Gyrometer.GetDefault() != null)
                    {
                        resultId += 2;
                        result.Append("G");
                    }

                    var accessStatus = await Geolocator.RequestAccessAsync();
                    if (accessStatus == GeolocationAccessStatus.Allowed)
                    {
                        resultId += 4;
                        result.Append("L");
                    }

                    if (Compass.GetDefault() != null)
                    {
                        resultId += 8;
                        result.Append("M");
                    }

                    if (OrientationSensor.GetDefault() != null)
                    {
                        resultId += 16;
                        result.Append("O");
                    }

                    if (LightSensor.GetDefault() != null)
                    {
                        resultId += 32;
                        result.Append("P");
                    }

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        ResultId = resultId,
                        Result = result.ToString()
                    });

                    break;
                }

                case "DATETIME":
                {
                    var utcNow = DateTime.UtcNow;
                    var now = DateTime.Now;
                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        ResultD = (utcNow - new DateTime(1970, 1, 1)).TotalSeconds,
                        Result = utcNow.ToString("s") + "Z",
                        Offset = TimeZoneInfo.Local.GetUtcOffset(now).TotalMinutes
                    });

                    break;
                }

                case "NAME":
                {
                    var deviceInfo = new EasClientDeviceInformation();

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = deviceInfo.FriendlyName
                    });

                    break;
                }

                case "OS":
                {
                    var deviceInfo = new EasClientDeviceInformation();

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = deviceInfo.OperatingSystem
                    });

                    break;
                }

                case "FWVER":
                {
                    var deviceInfo = new EasClientDeviceInformation();

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = deviceInfo.SystemFirmwareVersion
                    });

                    break;
                }

                case "HWVER":
                {
                    var deviceInfo = new EasClientDeviceInformation();

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = deviceInfo.SystemHardwareVersion
                    });

                    break;
                }


                case "PRODUCTNAME":
                {
                    var deviceInfo = new EasClientDeviceInformation();

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = deviceInfo.SystemProductName
                    });

                    break;
                }


                case "MANUFACTURER":
                {
                    var deviceInfo = new EasClientDeviceInformation();

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = deviceInfo.SystemManufacturer
                    });

                    break;
                }

                case "GET":
                {
                    if (string.IsNullOrWhiteSpace(devMessage.Key))
                    {
                        await SendResult(new DeviceResultMessage(devMessage) {ResultId = -2});
                        return;
                    }

                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        Result = appSettings.GetValueOrDefault(devMessage.Message, devMessage.Key)
                    });

                    break;
                }

                case "SET":
                {
                    if (string.IsNullOrWhiteSpace(devMessage.Key))
                    {
                        await SendResult(new DeviceResultMessage(devMessage) {ResultId = -2});
                        return;
                    }

                    var original = appSettings.GetValueOrDefault(string.Empty, devMessage.Key);
                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        ResultId = appSettings.AddOrUpdateValue(devMessage.Message, devMessage.Key) ? 0 : -1,
                        Result = original
                    });

                    appSettings.ReportChanged(devMessage.Key);

                    break;
                }

                case "DELETE":
                {
                    if (string.IsNullOrWhiteSpace(devMessage.Key))
                    {
                        await SendResult(new DeviceResultMessage(devMessage) {ResultId = -2});
                        return;
                    }

                    var result = appSettings.Remove(devMessage.Key);
                    await SendResult(new DeviceResultMessage(devMessage)
                    {
                        ResultId = result ? 0 : 1
                    });

                    break;
                }
            }
        }

        private async Task SendToastNotification(NotifyMessage notify)
        {
            var toastTemplate = ToastTemplateType.ToastImageAndText01;
            var toastXml = ToastNotificationManager.GetTemplateContent(toastTemplate);

            var toastTextElements = toastXml.GetElementsByTagName("text");
            toastTextElements[0].AppendChild(toastXml.CreateTextNode(notify.Message));

            if (!string.IsNullOrWhiteSpace(notify.Image))
            {
                var toastImageAttributes = toastXml.GetElementsByTagName("image");
                ((XmlElement) toastImageAttributes[0]).SetAttribute("src", notify.Image);
                ((XmlElement) toastImageAttributes[0]).SetAttribute("alt", "");
            }

            var toastNode = toastXml.SelectSingleNode("/toast");
            var audio = toastXml.CreateElement("audio");

            if (!string.IsNullOrWhiteSpace(notify.Audio))
            {
                if (notify.Audio.ToUpperInvariant().Equals("NONE"))
                {
                    audio.SetAttribute("silent", "true");
                }
                else
                {
                    audio.SetAttribute("src", notify.Audio);
                }

                toastNode.AppendChild(audio);
            }

            ((XmlElement) toastNode).SetAttribute("launch",
                "{\"Type\":\"toast\",\"Id\":\"" + notify.Id + "\",\"Tag\":\"" + (notify.Tag ?? string.Empty) +
                "\"}");

            var toast = new ToastNotification(toastXml);
            await
                dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { ToastNotificationManager.CreateToastNotifier().Show(toast); });
        }

        private async Task SendTileNotification(NotifyMessage notify)
        {
            var tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150ImageAndText01);

            UpdateTileParts(notify, tileXml);

            var tileNotification = new TileNotification(tileXml) {Tag = notify.Tag ?? notify.Id.ToString()};

            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);

            tileNotification.ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(10);

            await
                dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        try
                        {
                            TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);
                        }
                        catch (Exception)
                        {
                            //already updated is ok.
                        }
                    });
        }

        private static void UpdateTileParts(NotifyMessage notify, XmlDocument tileXml)
        {
            var squareTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text01);
            var squareTileTextAttributes = squareTileXml.GetElementsByTagName("text");
            squareTileTextAttributes[0].AppendChild(squareTileXml.CreateTextNode(notify.Message));

            var node = tileXml.ImportNode(squareTileXml.GetElementsByTagName("binding").Item(0), true);
            tileXml.GetElementsByTagName("visual").Item(0).AppendChild(node);

            if (!string.IsNullOrWhiteSpace(notify.Image))
            {
                var img = tileXml.GetElementsByTagName("image");
                ((XmlElement) img[0]).SetAttribute("src", notify.Image);
                ((XmlElement) img[0]).SetAttribute("alt", "");
            }
        }

        private async Task Play(TimingMessage playMessage)
        {
            var folders = new Dictionary<string, StorageFolder>
            {
                {"MUSIC:", KnownFolders.MusicLibrary},
                {"VIDEOS:", KnownFolders.VideosLibrary},
                {"PICTURES:", KnownFolders.PicturesLibrary},
                {"CAMERA:", KnownFolders.CameraRoll},
                {"SAVED:", KnownFolders.SavedPictures}
            };

            try
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    // force start of whatever is most recently sent
                    if (player.CurrentState != MediaElementState.Closed &&
                        player.CurrentState != MediaElementState.Stopped)
                    {
                        player.Stop();
                    }

                    if (player.Source != null)
                    {
                        player.Source = null;
                    }

                    StorageFile file = null;

                    var url = playMessage.Url;
                    var isWebUrl = false;

                    var colon = url.IndexOf(':');
                    if (colon > -1)
                    {
                        var root = url.Substring(0, colon + 1).ToUpperInvariant();

                        var folder = folders.SingleOrDefault(f => root.StartsWith(f.Key));
                        if (folder.Value != null)
                        {
                            file = await folder.Value.GetFileAsync(url.Substring(colon + 1));
                        }
                        else
                        {
                            isWebUrl = true;
                        }
                    }

                    if (isWebUrl)
                    {
                        player.Source = new Uri(url);
                    }
                    else
                    {
                        if (file == null)
                        {
                            await
                                SendResult(new ResultMessage(playMessage)
                                {
                                    ResultId = -3,
                                    Result = "file does not exist"
                                });
                            return;
                        }

                        var stream = await file.OpenAsync(FileAccessMode.Read);
                        player.SetSource(stream, file.ContentType);
                    }

                    player.Tag = playMessage;
                    player.CurrentStateChanged += Player_CurrentStateChanged;

                    player.Play();
                });
            }
            catch (Exception e)
            {
                await SendResult(new ResultMessage(playMessage) {ResultId = e.HResult, Result = e.Message});
            }
        }

        private async void Player_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            var msg = (TimingMessage) player.Tag;
            await
                SendResult(new ResultMessage(msg)
                {
                    ResultId = (int) player.CurrentState,
                    Result = Enum.GetName(typeof (MediaElementState), player.CurrentState)
                });
        }

        private async void Log(string message)
        {
            Debug.WriteLine(message);

            if (appSettings.IsLogging)
            {
                var now = DateTime.Now;
                logger.AppendLine(now.ToString("HH:mm:ss.fff:") + message);
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { appSettings.LogText = message; });
            }
        }

        private async Task<bool> SendToDestinations(IAddressable message, MemoryStream memStream,
            StorageFolder defaultFolder)
        {
            var sent = false;

            // pushes back for each destination
            foreach (var destination in destinations.Where(destination => destination.CheckPrefix(message.Url)))
            {
                memStream.Position = 0;
                var url = await destination.Send(memStream, message.Url);

                await SendResult(new ResultMessage(message as MessageBase) {Result = url, ResultId = 1});
                sent = true;
            }

            var colonIndex = message.Url?.IndexOf(':') ?? -2;
            if (!sent && (colonIndex == -1 || colonIndex > 1))
                // destination which is (drive-letter:) or direct filename.
            {
                try
                {
                    var namedFile = await defaultFolder.CreateFileAsync(message.Url,
                        CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBytesAsync(namedFile, memStream.ToArray());
                    sent = true;
                    await SendResult(new ResultMessage(message as MessageBase)
                    {
                        Result = namedFile.Name,
                        ResultId = 1
                    });
                }
                catch (Exception)
                {
                    // if issue in saving, continue with default
                }
            }

            return sent;
        }

        private async Task Record(TimingMessage message)
        {
            lock (keysInProcess)
            {
                keysInProcess["MICROPHONE"] = true;
            }

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await audio.InitializeAudioRecording();
                IRandomAccessStream stream;
                try
                {
                    appSettings.IsListening = true;
                    stream = await audio.CaptureAudio(new TimeSpan(0, 0, 0, 0, message.Ms));
                }
                finally
                {
                    appSettings.IsListening = false;
                }

                if (message.Autoplay.HasValue && message.Autoplay.Value)
                {
                    player.AutoPlay = true;
                    player.SetSource(stream, audio.GetFile().FileType);
                    player.Play();
                }

                //stores the image in Azure BLOB Storage
                var memStream = new MemoryStream();
                var fileStream = stream.AsStreamForRead();
                await fileStream.CopyToAsync(memStream);

                if (!await SendToDestinations(message, memStream, KnownFolders.VideosLibrary))
                {
                    await SendResult(new ResultMessage(message) {Result = audio.GetFile().Name, ResultId = 1});
                }
                else
                {
                    if (message.Keep == null || !message.Keep.Value)
                    {
                        //remove original
                        await fileStream.FlushAsync();
                        await audio.GetFile().DeleteAsync();
                    }
                }

                lock (keysInProcess)
                {
                    keysInProcess["MICROPHONE"] = false;
                }
            });
        }

        private void ToggleSensors(SensorMessage sensorsMessage)
        {
            Sensors.SensorSwitches.Id = sensorsMessage.Id;

            foreach (var sensorItem in sensorsMessage.Sensors)
            {
                if (sensorItem.A != null)
                {
                    sensorsMessage.Type = 'A';
                    if (Accelerometer.GetDefault() == null)
                    {
                        throw new UnsupportedSensorException("Accelerometer does not exist");
                    }

                    telemetry.TrackEvent("Sensor", new Dictionary<string, string> {{"A", sensorItem.A.Value.ToString()}});
                    Sensors.SensorSwitches.A = sensorItem.A.Value;
                }
                else if (sensorItem.G != null)
                {
                    sensorsMessage.Type = 'G';
                    if (Gyrometer.GetDefault() == null)
                    {
                        throw new UnsupportedSensorException("Gyrometer does not exist");
                    }

                    telemetry.TrackEvent("Sensor", new Dictionary<string, string> { { "G", sensorItem.G.Value.ToString() } });
                    Sensors.SensorSwitches.G = sensorItem.G.Value;
                }
                else if (sensorItem.M != null)
                {
                    sensorsMessage.Type = 'M';
                    if (Compass.GetDefault() == null)
                    {
                        throw new UnsupportedSensorException("Compass does not exist");
                    }

                    telemetry.TrackEvent("Sensor", new Dictionary<string, string> { { "M", sensorItem.M.Value.ToString() } });
                    Sensors.SensorSwitches.M = sensorItem.M.Value;
                }
                else if (sensorItem.L != null)
                {
                    sensorsMessage.Type = 'L';
                    Sensors.SensorSwitches.L = sensorItem.L.Value;
                }
                else if (sensorItem.Q != null)
                {
                    sensorsMessage.Type = 'Q';
                    if (OrientationSensor.GetDefault() == null)
                    {
                        throw new UnsupportedSensorException("OrientationSensor does not exist");
                    }

                    telemetry.TrackEvent("Sensor", new Dictionary<string, string> { { "Q", sensorItem.Q.Value.ToString() } });
                    Sensors.SensorSwitches.Q = sensorItem.Q.Value;
                }
                else if (sensorItem.P != null)
                {
                    sensorsMessage.Type = 'P';
                    if (LightSensor.GetDefault() == null)
                    {
                        throw new UnsupportedSensorException("LightSensor does not exist");
                    }

                    telemetry.TrackEvent("Sensor", new Dictionary<string, string> { { "P", sensorItem.P.Value.ToString() } });
                    Sensors.SensorSwitches.P = sensorItem.P.Value;
                }

                //outside of scope - applies to last only
                Sensors.SensorSwitches.Delta = sensorItem.Delta;
                Sensors.SensorSwitches.Interval = sensorItem.Interval;
            }

            Sensors.Start();
        }

        private void Vibrate(TimingMessage timingMessage)
        {
            var vibrationDevice = VibrationDevice.GetDefault();
            vibrationDevice.Vibrate(new TimeSpan(0, 0, 0, timingMessage.Ms/1000, timingMessage.Ms%1000));
        }

        private async Task TakePicture(CameraMessage cameraMessage)
        {
            if (isCameraInitializing)
            {
                await SendResult(new ResultMessage(cameraMessage) {ResultId = -3, Result = "Initializing"});
                return;
            }

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                lock (keysInProcess)
                {
                    keysInProcess["CAMERA"] = true;
                }

                await InitializeCamera();
                
                var imageName = "photo_" + DateTime.Now.Ticks + ".jpg";
                foreach (
                    var destination in destinations.Where(destination => destination.CheckPrefix(cameraMessage.Url)))
                {
                    imageName = destination.ParseAddressForFileName(cameraMessage.Url);
                    break;
                }

                StorageFile stream = null;
                try
                {
                    var timeout = DateTime.Now.AddSeconds(5);
                    while (!camera.isPreviewing && DateTime.Now < timeout)
                    {
                        await Task.Delay(250);
                    }

                    stream = await camera.Capture(imageName);
                }
                catch (Exception e)
                {
                    await SendResult(new ResultMessage(cameraMessage) {ResultId = -99, Result = e.Message });
                    lock (keysInProcess)
                    {
                        keysInProcess["CAMERA"] = false;
                    }

                    return;
                }

                //stores the image in Azure BLOB Storage
                var memStream = new MemoryStream();
                var fileStream = await stream.OpenStreamForReadAsync();
                await fileStream.CopyToAsync(memStream);

                if (!await SendToDestinations(cameraMessage, memStream, KnownFolders.PicturesLibrary))
                {
                    await SendResult(new ResultMessage(cameraMessage) {Result = imageName});
                }

                lock (keysInProcess)
                {
                    keysInProcess["CAMERA"] = false;
                }
            });
        }

        private async void Recognize(SpeechRecognitionMessage speech)
        {
            if (speechService == null)
            {
                speechService = new Speech();
                speechService.SpeechStatusChanged +=
                    (sender, args) => { appSettings.IsListening = args.Status == SpeechStatus.Listening; };
            }

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (speech.Action != null && speech.Action.Equals("STOP"))
                    {
                        speechService.Stop();
                        return;
                    }

                    var expectedConfidence = speech.Confidence ?? (int) SpeechRecognitionConfidence.Medium;

                    var recognizeText = default(RecognizedSpeech);

                    var confident = false;
                    var timeout = DateTime.UtcNow.AddMilliseconds(speech.Ms ?? 0);
                    while (!confident && (speech.Ms == null || speech.Ms == 0 || DateTime.UtcNow < timeout))
                        //consider timeout here
                    {
                        Debug.WriteLine("recognizing...");
                        recognizeText = await speechService.Recognize(speech.Message, speech.UI);
                        Debug.WriteLine("end recognizing...");
                        if (recognizeText.status != SpeechRecognitionResultStatus.Success)
                        {
                            break;
                        }

                        confident = expectedConfidence >= recognizeText.confidence;
                    }

                    var status = recognizeText.status == SpeechRecognitionResultStatus.Success
                        ? recognizeText.index
                        : (recognizeText.status == SpeechRecognitionResultStatus.UserCanceled
                            ? 0
                            : -(int) recognizeText.status);

                    if ((recognizeText.status == SpeechRecognitionResultStatus.Unknown ||
                         recognizeText.status == SpeechRecognitionResultStatus.Success)
                        && !confident && (speech.Ms != null && speech.Ms != 0 || DateTime.UtcNow >= timeout))
                    {
                        status = 0; //timeout or cancelled
                    }

                    await
                        SendResult(new SpeechResultMessage(speech)
                        {
                            Result = recognizeText.text,
                            ResultId = status,
                            Action = recognizeText.action,
                            Value = recognizeText.confidence
                        });
                }
                catch (Exception e)
                {
                    await SendResult(new ResultMessage
                        (speech) {Result = e.Message, ResultId = e.HResult});
                }
            });
        }

        private async void Speak(SpeechMessage speech)
        {
            var service = new Speech();

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (speech.Action != null && speech.Action.Equals("STOP"))
                    {
                        player?.Stop();
                    }
                    else
                    {
                        service.Speak(player, speech);
                    }
                }
                catch (Exception e)
                {
                    await SendResult(new ResultMessage(speech)
                    {
                        Result = e.Message,
                        ResultId = e.HResult
                    });
                }
            });
        }

        internal async Task SendEvent(object sender, RoutedEventArgs e, string action, MessageBase message = null,
            string messageText = null)
        {
            if (sender is Grid && sensors.ContainsKey("LCDG:TOUCH"))
            {
                if (e is PointerRoutedEventArgs)
                {
                    var pe = (PointerRoutedEventArgs) e;
                    var pt = pe.GetCurrentPoint(sender as FrameworkElement);
                    await SendResult(new ScreenResultMessage(message)
                    {
                        Area = "TOUCH",
                        Action = action,
                        X = pt.Position.X,
                        Y = pt.Position.Y
                    });
                }
            }
            else if (!(sender is Grid))
            {
                var source = (e.OriginalSource ?? sender) as FrameworkElement;

                var id = (int) source.GetValue(Screen.RemoteIdProperty);

                if (message == null)
                {
                    message = new ScreenMessage
                    {
                        Type = 'S',
                        Service = "LCDG",
                        Id = id
                    };
                }

                var newid = (message.Id == 0) ? id : message.Id;

                await SendResult(new ScreenResultMessage(message)
                {
                    Area = source.GetType().Name.ToUpperInvariant(),
                    Action = action,
                    Tag = source.Tag?.ToString(),
                    Type = message.Type ?? 'S',
                    Id = newid,
                    Result = messageText
                }, newid + action);
            }
        }
    }
}