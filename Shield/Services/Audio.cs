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
namespace Shield.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    using Windows.Media;
    using Windows.Media.Capture;
    using Windows.Media.MediaProperties;
    using Windows.Storage;
    using Windows.Storage.Streams;

    public class Audio
    {
        private MediaCapture _mediaCaptureManager;

        private StorageFile _recordStorageFile;

        public async Task InitializeAudioRecording()
        {
            this._mediaCaptureManager = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings();
            settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            settings.MediaCategory = MediaCategory.Other;
            settings.AudioProcessing = AudioProcessing.Default;

            await this._mediaCaptureManager.InitializeAsync(settings);

            Debug.WriteLine("Device initialized successfully");
            this._mediaCaptureManager.RecordLimitationExceeded += this.RecordLimitationExceeded;
            this._mediaCaptureManager.Failed += this.Failed;
        }

        private void RecordLimitationExceeded(MediaCapture sender)
        {
            throw new NotImplementedException();
        }

        private void Failed(MediaCapture sender, MediaCaptureFailedEventArgs erroreventargs)
        {
            throw new NotImplementedException();
        }

        public async Task<IRandomAccessStream> CaptureAudio(TimeSpan timespan)
        {
            await this.CaptureAudio();
            await Task.Delay(timespan);
            return await this.StopCapture();
        }

        public StorageFile GetFile()
        {
            return this._recordStorageFile;
        }

        private async Task CaptureAudio()
        {
            try
            {
                Debug.WriteLine("Starting record");
                var fileName = "audio.wav";

                this._recordStorageFile =
                    await
                    KnownFolders.VideosLibrary.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                Debug.WriteLine("Create record file successfully");

                var recordProfile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Auto);

                await this._mediaCaptureManager.StartRecordToStorageFileAsync(recordProfile, this._recordStorageFile);

                Debug.WriteLine("Start Record successful");
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to capture audio:" + e.Message);
            }
        }

        private async Task<IRandomAccessStream> StopCapture()
        {
            Debug.WriteLine("Stopping recording");
            await this._mediaCaptureManager.StopRecordAsync();
            Debug.WriteLine("Stop recording successful");

            var stream = await this._recordStorageFile.OpenAsync(FileAccessMode.Read);

            return stream;
        }
    }
}