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
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using Shield.Core;
using Shield.Core.Models;

namespace Shield.Services
{
    public class Screen
    {
        public static readonly DependencyProperty RemoteIdProperty = DependencyProperty.RegisterAttached("RemoteId",
            typeof (int), typeof (FrameworkElement), new PropertyMetadata(0));

        private readonly int DefaultFontSize = 22;

        private readonly FontFamily fixedFont = new FontFamily("Courier New");
        private readonly SolidColorBrush foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        private readonly SolidColorBrush gray = new SolidColorBrush(Color.FromArgb(0xFF, 0x80, 0x80, 0x80));
        private readonly MainPage mainPage;

        private readonly SolidColorBrush textForgroundBrush = new SolidColorBrush(Colors.White);
        private SolidColorBrush background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private int lastY = -1;

        public Screen(MainPage mainPage)
        {
            this.mainPage = mainPage;
        }

        public async Task LogPrint(ScreenMessage lcdt)
        {
            if (lcdt.Action != null && lcdt.Action.ToUpperInvariant().Equals("CLEAR"))
            {
                mainPage.text.Text = "";
            }

            await
                mainPage.dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => { mainPage.text.Text += lcdt.Message + "\r\n"; });
        }

        public async void LcdPrint(ScreenMessage lcdt)
        {
            var isText = lcdt.Service.Equals("LCDT");
            FrameworkElement element = null;
            var expandToEdge = false;
            SolidColorBrush backgroundBrush = null;

            if (lcdt.Action != null)
            {
                if (!string.IsNullOrWhiteSpace(lcdt.ARGB))
                {
                    if (lcdt.ARGB[0] == '#')
                    {
                        var hex = lcdt.ARGB.ToByteArray();
                        if (hex.Length > 3)
                        {
                            backgroundBrush =
                                new SolidColorBrush(Color.FromArgb((hex[0] == 0 ? (byte) 255 : hex[0]), hex[1], hex[2],
                                    hex[3]));
                        }
                    }
                    else
                    {
                        uint color;
                        if (uint.TryParse(lcdt.ARGB, out color))
                        {
                            var argb = new ArgbUnion {Value = color};
                            backgroundBrush =
                                new SolidColorBrush(Color.FromArgb(argb.A == 0 ? (byte) 255 : argb.A, argb.R, argb.G,
                                    argb.B));
                        }
                    }
                }

                var action = lcdt.Action.ToUpperInvariant();
                switch (action)
                {
                    case "ORIENTATION":
                    {
                        var current = DisplayInformation.AutoRotationPreferences;
                        if (lcdt.Value.HasValue)
                        {
                            DisplayInformation.AutoRotationPreferences = (DisplayOrientations) lcdt.Value.Value;
                        }

                        await mainPage.SendResult(new ScreenResultMessage(lcdt) {ResultId = (int) current});

                        break;
                    }
                    case "ENABLE":
                    {
                        mainPage.sensors[lcdt.Service + ":" + lcdt.Message] = 1;
                        return;
                    }
                    case "DISABLE":
                    {
                        if (mainPage.sensors.ContainsKey(lcdt.Service + ":" + lcdt.Message))
                        {
                            mainPage.sensors.Remove(lcdt.Service + ":" + lcdt.Message);
                        }

                        return;
                    }
                    case "CLEAR":
                    {
                        if (lcdt.Y.HasValue)
                        {
                            RemoveLine(lcdt.Y.Value);
                        }
                        else if (lcdt.Pid.HasValue)
                        {
                            RemoveId(lcdt.Pid.Value);
                        }
                        else
                        {
                            mainPage.canvas.Children.Clear();

                            if (backgroundBrush != null)
                            {
                                mainPage.canvas.Background = backgroundBrush;
                            }

                            lastY = -1;
                            mainPage.player.Stop();
                            mainPage.player.Source = null;
                        }

                        break;
                    }

                    case "BUTTON":
                    {
                        element = new Button
                        {
                            Content = lcdt.Message,
                            FontSize = lcdt.Size ?? DefaultFontSize,
                            Tag = lcdt.Tag,
                            Foreground = textForgroundBrush,
                            Background = new SolidColorBrush(Colors.Gray)
                        };

                        element.Tapped += async (s, a) => await mainPage.SendEvent(s, a, "tapped");
                        ((Button) element).Click += async (s, a) => await mainPage.SendEvent(s, a, "click");
                        element.PointerPressed += async (s, a) => await mainPage.SendEvent(s, a, "pressed");
                        element.PointerReleased += async (s, a) => await mainPage.SendEvent(s, a, "released");

                        break;
                    }

                    case "IMAGE":
                    {
                        var imageBitmap = new BitmapImage(new Uri(lcdt.Path, UriKind.Absolute));
                        //imageBitmap.CreateOptions = Windows.UI.Xaml.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;

                        if (lcdt.Width.HasValue)
                        {
                            imageBitmap.DecodePixelWidth = lcdt.Width.Value;
                        }

                        if (lcdt.Height.HasValue)
                        {
                            imageBitmap.DecodePixelHeight = lcdt.Height.Value;
                        }

                        element = new Image
                        {
                            Tag = lcdt.Tag
                        };

                        ((Image) element).Source = imageBitmap;

                        element.Tapped += async (s, a) => await mainPage.SendEvent(s, a, "tapped");
                        break;
                    }
                    case "LINE":
                    {
                        var line = new Line
                        {
                            X1 = lcdt.X.Value,
                            Y1 = lcdt.Y.Value,
                            X2 = lcdt.X2.Value,
                            Y2 = lcdt.Y2.Value,
                            StrokeThickness = lcdt.Width ?? 1,
                            Stroke = foreground
                        };

                        element = line;

                        break;
                    }

                    case "INPUT":
                    {
                        element = new TextBox
                        {
                            Text = lcdt.Message ?? string.Empty,
                            FontSize = lcdt.Size ?? DefaultFontSize,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = textForgroundBrush,
                            AcceptsReturn = lcdt.Multi ?? false
                        };

                        expandToEdge = true;

                        element.SetValue(Canvas.LeftProperty, lcdt.X);
                        element.SetValue(Canvas.TopProperty, lcdt.Y);

                        element.LostFocus +=
                            async (s, a) => await mainPage.SendEvent(s, a, "lostfocus", lcdt, ((TextBox) s).Text);
                        ((TextBox) element).TextChanged +=
                            async (s, a) => await mainPage.SendEvent(s, a, "changed", lcdt, ((TextBox) s).Text);

                        break;
                    }
                    case "RECTANGLE":
                    {
                        var rect = new Rectangle
                        {
                            Tag = lcdt.Tag,
                            Fill = backgroundBrush ?? gray
                        };

                        if (lcdt.Width.HasValue)
                        {
                            rect.Width = lcdt.Width.Value;
                        }

                        if (lcdt.Height.HasValue)
                        {
                            rect.Height = lcdt.Height.Value;
                        }

                        element = rect;

                        element.Tapped += async (s, a) => await mainPage.SendEvent(s, a, "tapped", lcdt);
                        rect.PointerEntered += async (s, a) => await mainPage.SendEvent(s, a, "entered", lcdt);
                        rect.PointerExited += async (s, a) => await mainPage.SendEvent(s, a, "exited", lcdt);

                        break;
                    }
                    case "TEXT":
                    {
                        var textBlock = new TextBlock
                        {
                            Text = lcdt.Message,
                            FontSize = lcdt.Size ?? DefaultFontSize,
                            TextWrapping = TextWrapping.Wrap,
                            Tag = lcdt.Tag,
                            Foreground = textForgroundBrush
                        };

                        expandToEdge = true;

                        element = textBlock;
                        element.SetValue(Canvas.LeftProperty, lcdt.X);
                        element.SetValue(Canvas.TopProperty, lcdt.Y);
                        break;
                    }

                    default:
                        break;
                }
            }

            if (element == null && isText && lcdt.Message != null)
            {
                var x = lcdt.X ?? 0;
                var y = lcdt.Y ?? lastY + 1;

                expandToEdge = true;

                element = new TextBlock
                {
                    Text = lcdt.Message,
                    FontSize = lcdt.Size ?? DefaultFontSize,
                    TextWrapping = TextWrapping.Wrap,
                    Tag = y.ToString(),
                    Foreground = textForgroundBrush
                };

                var textblock = (TextBlock) element;

                textblock.FontFamily = fixedFont;

                if (lcdt.Foreground != null)
                {
                    textblock.Foreground = HexColorToBrush(lcdt.Foreground);
                }

                if (lcdt.HorizontalAlignment != null)
                {
                    if (lcdt.HorizontalAlignment.Equals("Center"))
                    {
                        textblock.TextAlignment = TextAlignment.Center;
                    }
                }

                element.SetValue(Canvas.LeftProperty, isText ? x*textblock.FontSize : x);
                element.SetValue(Canvas.TopProperty, isText ? y*textblock.FontSize : y);
            }
            else if (element != null && element.GetType() != typeof (Line))
            {
                element.SetValue(Canvas.LeftProperty, lcdt.X);
                element.SetValue(Canvas.TopProperty, lcdt.Y);
            }

            if (element != null)
            {
                var x = lcdt.X ?? 0;
                var y = lcdt.Y ?? lastY + 1;

                if (lcdt.HorizontalAlignment != null)
                {
                    if (lcdt.HorizontalAlignment.Equals("Center"))
                    {
                        element.HorizontalAlignment = HorizontalAlignment.Center;
                        element.Width = mainPage.canvas.Width;
                    }
                }

                if (lcdt.FlowDirection != null)
                {
                    if (lcdt.FlowDirection.Equals("RightToLeft"))
                    {
                        element.FlowDirection = FlowDirection.RightToLeft;
                    }
                    else if (lcdt.FlowDirection.Equals("LeftToRight"))
                    {
                        element.FlowDirection = FlowDirection.LeftToRight;
                    }
                }

                if (lcdt.Width.HasValue)
                {
                    element.Width = lcdt.Width.Value;
                }
                else if (expandToEdge)
                {
                    element.Width = mainPage.canvas.ActualWidth;
                }

                if (lcdt.Height.HasValue)
                {
                    element.Height = lcdt.Height.Value;
                }

                //TODO: add optional/extra properties in a later version here.
                if (isText && x == 0)
                {
                    RemoveLine(y);
                }

                element.SetValue(RemoteIdProperty, lcdt.Id);

                mainPage.canvas.Children.Add(element);

                if (isText)
                {
                    lastY = y;
                }
            }
        }


        public static Brush HexColorToBrush(string color)
        {
            color = color.Replace("#", "");
            if (color.Length > 5)
            {
                return new SolidColorBrush(ColorHelper.FromArgb(
                    color.Length > 7
                        ? byte.Parse(color.Substring(color.Length - 8, 2), NumberStyles.HexNumber)
                        : (byte) 255,
                    byte.Parse(color.Substring(color.Length - 6, 2), NumberStyles.HexNumber),
                    byte.Parse(color.Substring(color.Length - 4, 2), NumberStyles.HexNumber),
                    byte.Parse(color.Substring(color.Length - 2, 2), NumberStyles.HexNumber)));
            }
            return null;
        }

        private void RemoveLine(int y)
        {
            var lines =
                mainPage.canvas.Children.Where(
                    t => t is TextBlock && ((TextBlock) t).Tag != null && ((TextBlock) t).Tag.Equals(y.ToString()));
            foreach (var line in lines)
            {
                mainPage.canvas.Children.Remove(line);
            }
        }

        private UIElement GetId(int id)
        {
            return
                mainPage.canvas.Children.FirstOrDefault(e => ((int) e.GetValue(RemoteIdProperty)) == id);
        }

        private void RemoveId(int id)
        {
            var items =
                mainPage.canvas.Children.Where(e => ((int) e.GetValue(RemoteIdProperty)) == id);

            foreach (var item in items)
            {
                mainPage.canvas.Children.Remove(item);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct ArgbUnion
        {
            [FieldOffset(0)] public byte B;
            [FieldOffset(1)] public byte G;
            [FieldOffset(2)] public byte R;
            [FieldOffset(3)] public byte A;

            [FieldOffset(0)] public uint Value;
        }
    }
}