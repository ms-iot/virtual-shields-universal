using System;
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