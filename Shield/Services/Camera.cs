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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Panel = Windows.Devices.Enumeration.Panel;

namespace Shield.Services //CUT this down
{
    public class Camera
    {
        public enum CameraResolutionFormat
        {
            Unknown = -1,

            FourByThree = 0,

            SixteenByNine = 1
        }

        public enum DisplayAspectRatio
        {
            Unknown = -1,

            FifteenByNine = 0,

            SixteenByNine = 1
        }

        public MediaCapture captureManager;
        public bool isAutoFocus = false;
        public bool isPreviewing;

        public async Task InitializePreview(CaptureElement captureElement)
        {
            captureManager = new MediaCapture();

            var cameraID = await GetCameraID(Panel.Back);

            if (cameraID == null)
            {
                return;
            }

            await captureManager.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.Photo,
                AudioDeviceId = string.Empty,
                VideoDeviceId = cameraID.Id
            });

            await
                captureManager.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.Photo,
                    maxResolution());

            captureManager.SetPreviewRotation(VideoRotation.Clockwise90Degrees);

            StartPreview(captureElement);
        }

        private async void StartPreview(CaptureElement captureElement)
        {
            captureElement.Visibility = Visibility.Visible;
            captureElement.Source = captureManager;

            await captureManager.StartPreviewAsync();

            isPreviewing = true;

            if (GetDisplayAspectRatio() == DisplayAspectRatio.FifteenByNine)
            {
                GetFifteenByNineBounds();
            }
        }

        private DisplayAspectRatio GetDisplayAspectRatio()
        {
            var result = DisplayAspectRatio.Unknown;

            //WP8.1 uses logical pixel dimensions, we need to convert this to raw pixel dimensions
            var logicalPixelWidth = Window.Current.Bounds.Width;
            var logicalPixelHeight = Window.Current.Bounds.Height;

            var rawPerViewPixels = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            var rawPixelHeight = logicalPixelHeight*rawPerViewPixels;
            var rawPixelWidth = logicalPixelWidth*rawPerViewPixels;

            //calculate and return screen format
            var relation = Math.Max(rawPixelWidth, rawPixelHeight)/Math.Min(rawPixelWidth, rawPixelHeight);
            if (Math.Abs(relation - (15.0/9.0)) < 0.01)
            {
                result = DisplayAspectRatio.FifteenByNine;
            }
            else if (Math.Abs(relation - (16.0/9.0)) < 0.01)
            {
                result = DisplayAspectRatio.SixteenByNine;
            }

            return result;
        }

        /// <summary>
        ///     Helper to get the correct Bounds for 15:9 screens and to set finalPhotoAreaBorder values
        /// </summary>
        /// <returns></returns>
        private BitmapBounds GetFifteenByNineBounds()
        {
            var bounds = new BitmapBounds();

            //image size is raw pixels, so we need also here raw pixels
            var logicalPixelWidth = Window.Current.Bounds.Width;
            var logicalPixelHeight = Window.Current.Bounds.Height;

            var rawPerViewPixels = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            var rawPixelHeight = logicalPixelHeight*rawPerViewPixels;
            var rawPixelWidth = logicalPixelWidth*rawPerViewPixels;

            //calculate scale factor of UniformToFill Height (remember, we rotated the preview)
            var scaleFactorVisualHeight = maxResolution().Width/rawPixelHeight;

            //calculate the visual Width 
            //(because UniFormToFill scaled the previewElement Width down to match the previewElement Height)
            var visualWidth = maxResolution().Height/scaleFactorVisualHeight;

            //calculate cropping area for 15:9
            var scaledBoundsWidth = maxResolution().Height;
            var scaledBoundsHeight = (scaledBoundsWidth/9)*15;

            //we are starting at the top of the image
            bounds.Y = 0;
            //cropping the image width
            bounds.X = 0;
            bounds.Height = scaledBoundsHeight;
            bounds.Width = scaledBoundsWidth;

            //set finalPhotoAreaBorder values that shows the user the area that is captured
            //finalPhotoAreaBorder.Width = (scaledBoundsWidth / scaleFactorVisualHeight) / rawPerViewPixels;
            //finalPhotoAreaBorder.Height = (scaledBoundsHeight / scaleFactorVisualHeight) / rawPerViewPixels;
            //finalPhotoAreaBorder.Margin = new Thickness(
            //                                Math.Floor(((rawPixelWidth - visualWidth) / 2) / rawPerViewPixels),
            //                                0,
            //                                Math.Floor(((rawPixelWidth - visualWidth) / 2) / rawPerViewPixels),
            //                                0);
            //finalPhotoAreaBorder.Visibility = Visibility.Visible;

            return bounds;
        }

        private static async Task<DeviceInformation> GetCameraID(Panel camera)
        {
            var deviceID = (await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture))
                .FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == camera);

            return deviceID;
        }

        private CameraResolutionFormat MatchScreenFormat(Size resolution)
        {
            var result = CameraResolutionFormat.Unknown;

            var relation = Math.Max(resolution.Width, resolution.Height)/Math.Min(resolution.Width, resolution.Height);
            if (Math.Abs(relation - (4.0/3.0)) < 0.01)
            {
                result = CameraResolutionFormat.FourByThree;
            }
            else if (Math.Abs(relation - (16.0/9.0)) < 0.01)
            {
                result = CameraResolutionFormat.SixteenByNine;
            }

            return result;
        }

        private VideoEncodingProperties maxResolution()
        {
            VideoEncodingProperties resolutionMax = null;

            //get all photo properties
            var resolutions =
                captureManager.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.Photo);

            //generate new list to work with
            var vidProps = new List<VideoEncodingProperties>();

            //add only those properties that are 16:9 to our own list
            for (var i = 0; i < resolutions.Count; i++)
            {
                var res = (VideoEncodingProperties) resolutions[i];

                if (MatchScreenFormat(new Size(res.Width, res.Height)) != CameraResolutionFormat.FourByThree)
                {
                    vidProps.Add(res);
                }
            }

            //order the list, and select the highest resolution that fits our limit
            if (vidProps.Count != 0)
            {
                vidProps = vidProps.OrderByDescending(r => r.Width).ToList();

                resolutionMax = vidProps.Where(r => r.Width < 2600).First();
            }

            return resolutionMax;
        }

        public async Task<StorageFile> Capture(string imageName)
        {
            return await CapturePreviewWithoutModifications(imageName);
        }

        private async Task<StorageFile> CapturePreviewWithoutModifications(string imageName)
        {
            //declare image format
            var format = ImageEncodingProperties.CreateJpeg();

            //generate file in local folder:
            //StorageFile capturefile = await ApplicationData.Current.LocalFolder.CreateFileAsync("photo_" + DateTime.Now.Ticks.ToString(), CreationCollisionOption.ReplaceExisting);
            var capturefile =
                await KnownFolders.CameraRoll.CreateFileAsync(imageName, CreationCollisionOption.ReplaceExisting);

            //take & save photo
            await captureManager.CapturePhotoToStorageFileAsync(format, capturefile);

            //show captured photo
            var img = new BitmapImage(new Uri(capturefile.Path));

            //canvasBackground.Source = img;
            //takenImage.Visibility = Visibility.Visible;

            //return img;
            return capturefile;
        }

        private async void CaptureSixteenByNineImage()
        {
            //declare string for filename
            var captureFileName = string.Empty;
            //declare image format
            var format = ImageEncodingProperties.CreateJpeg();

            //rotate and save the image
            using (var imageStream = new InMemoryRandomAccessStream())
            {
                //generate stream from MediaCapture
                await captureManager.CapturePhotoToStreamAsync(format, imageStream);

                //create decoder and encoder
                var dec = await BitmapDecoder.CreateAsync(imageStream);
                var enc = await BitmapEncoder.CreateForTranscodingAsync(imageStream, dec);

                //roate the image
                enc.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;

                //write changes to the image stream
                await enc.FlushAsync();

                //save the image
                var folder = KnownFolders.SavedPictures;
                var capturefile =
                    await
                        folder.CreateFileAsync("photo_" + DateTime.Now.Ticks + ".jpg",
                            CreationCollisionOption.ReplaceExisting);
                captureFileName = capturefile.Name;

                //store stream in file
                using (var fileStream = await capturefile.OpenStreamForWriteAsync())
                {
                    try
                    {
                        //because of using statement stream will be closed automatically after copying finished
                        await RandomAccessStream.CopyAsync(imageStream, fileStream.AsOutputStream());
                    }
                    catch
                    {
                    }
                }
            }
            CleanCapture();

            //load saved image
            //LoadCapturedphoto(captureFileName);
        }

        private async void CleanCapture()
        {
            if (captureManager != null)
            {
                if (isPreviewing)
                {
                    await captureManager.StopPreviewAsync();
                    isPreviewing = false;
                }
                captureManager.Dispose();

                //previewElement.Source = null;
                //previewElement.Visibility = Visibility.Collapsed;
                //takenImage.Source = null;
                //takenImage.Visibility = Visibility.Collapsed;
                //captureButton.Content = "capture";
            }
        }
    }
}