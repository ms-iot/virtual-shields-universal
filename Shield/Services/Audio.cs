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

﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Shield.Services
{
    public class Audio
    {
        private MediaCapture _mediaCaptureManager;
        private StorageFile _recordStorageFile;

        public async Task InitializeAudioRecording()
        {
            _mediaCaptureManager = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings();
            settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            settings.MediaCategory = MediaCategory.Other;
            settings.AudioProcessing = AudioProcessing.Default;

            await _mediaCaptureManager.InitializeAsync(settings);

            Debug.WriteLine("Device initialized successfully");
            _mediaCaptureManager.RecordLimitationExceeded += RecordLimitationExceeded;
            _mediaCaptureManager.Failed += Failed;
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
            await CaptureAudio();
            await Task.Delay(timespan);
            return await StopCapture();
        }

        public StorageFile GetFile()
        {
            return _recordStorageFile;
        }

        private async Task CaptureAudio()
        {
            try
            {
                Debug.WriteLine("Starting record");
                var fileName = "audio.wav";

                _recordStorageFile =
                    await
                        KnownFolders.VideosLibrary.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                Debug.WriteLine("Create record file successfully");

                var recordProfile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Auto);

                await _mediaCaptureManager.StartRecordToStorageFileAsync(recordProfile, _recordStorageFile);

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
            await _mediaCaptureManager.StopRecordAsync();
            Debug.WriteLine("Stop recording successful");

            var stream = await _recordStorageFile.OpenAsync(FileAccessMode.Read);

            return stream;
        }
    }
}