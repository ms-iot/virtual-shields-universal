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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Shield.Core;

namespace Shield.Services
{
    public class DataItem : DependencyObject, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof (double), typeof (DataItem),
                new PropertyMetadata(0d, OnValueChanged));

        public string Name { get; set; }

        public double Value
        {
            get { return (double) GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs a)
        {
            var di = d as DataItem;
            di.NotifyPropertyChanged("Value");
        }

        private void NotifyPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }

    public class DataItems : ObservableCollection<DataItem>
    {
        public void Refresh()
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public class XYZ : DependencyObject
    {
        public XYZ()
        {
        }

        public XYZ(object source, string name, string tag, string[] names)
        {
            Source = source;
            Name = name;
            Tag = tag;

            Data = new DataItems();
            foreach (var item in names)
            {
                var newitem = new DataItem {Name = item, Value = 0};
                newitem.PropertyChanged += SubPropertyChanged;

                Data.Add(newitem);
            }
        }

        //public double X { get; set; }

        //public double Y { get; set; }
        //public double Z { get; set; }
        //public double W { get; set; }
        public int Id { get; set; }

        public object Source { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public double Delta { get; set; }
        public DataItems Data { get; set; }
        public bool IsChanged = false;

        private bool IsWithinDelta(double a, double b)
        {
            return Math.Abs(b - a) < this.Delta;
        }

        public XYZ New(double x, double y, double z, double w)
        {
            var item = this; //ew XYZ() { Source = Source, Data = Data, Name = Name, Tag = Tag };

            if (this.Delta > 0 && IsWithinDelta(x, item.Data[0].Value) && IsWithinDelta(y, item.Data[1].Value) &&
                IsWithinDelta(z, item.Data[2].Value)
                && (item.Data.Count() < 4 || IsWithinDelta(w, item.Data[3].Value)))
            {
                IsChanged = false;
                return this; //no change
            }

            IsChanged = true;

            item.Data[0].Value = x;
            item.Data[1].Value = y;
            item.Data[2].Value = z;

            if (item.Data.Count() > 3)
            {
                item.Data[3].Value = w;
            }

            item.Data.Refresh();

            return item;
        }

        public XYZ New()
        {
            IsChanged = true;
            return this;
        }

        private void SubPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
        }
    }

    public class Sensors : ObservableCollection<XYZ>
    {
        public delegate void SensorUpdated(XYZ data);

        private const int ACCELERATOR = 0;
        private const int GYROSCOPE = 1;
        private const int COMPASS = 2;
        private const int LOCATION = 3;
        private const int QUANTIZATION = 4;
        private const int LIGHTSENSOR = 5;
        private readonly CoreDispatcher dispatcher;
        private Accelerometer accelerometer;
        private Compass compass;
        private Geolocator geolocator;
        private Gyrometer gyrometer;
        private LightSensor lightSensor;
        private OrientationSensor orientation;

        public Sensors()
        {
            Add(new XYZ(accelerometer, "Accelerometer", "A", new[] {"X", "Y", "Z"}));
            Add(new XYZ(gyrometer, "Gyroscope", "G", new[] {"X", "Y", "Z"}));
            Add(new XYZ(compass, "Compass", "M", new[] {"Mag", "True"}));
            Add(new XYZ(geolocator, "Location", "L", new[] {"Lat", "Lon", "Alt"}));
            Add(new XYZ(orientation, "Orientation", "Q", new[] {"X", "Y", "Z", "W"}));
            Add(new XYZ(lightSensor, "LightSensor", "P", new[] {"Lux"}));
        }

        public Sensors(CoreDispatcher dispatcher) : this()
        {
            this.dispatcher = dispatcher;
            SensorSwitches = new SensorSwitches();
        }

        public Sensors ItemsList => this;
        public SensorSwitches SensorSwitches { get; set; }
        public bool IsSending { get; set; }
        public event SensorUpdated OnSensorUpdated;

        public async void NewLight(LightSensor sender, LightSensorReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this[LIGHTSENSOR] = reading == null
                    ? this[LIGHTSENSOR].New()
                    : this[LIGHTSENSOR].New(reading.IlluminanceInLux, 0, 0, 0);
                if (this[LIGHTSENSOR].IsChanged)
                {
                    OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                    OnSensorUpdated?.Invoke(this[LIGHTSENSOR]);
                }
            });

            if (SensorSwitches.P.HasValue && (SensorSwitches.P.Value == 1 || SensorSwitches.P.Value == 3))
            {
                SensorSwitches.P = 0;
            }
        }

        public async void NewAcc(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this[ACCELERATOR] = reading == null
                    ? this[ACCELERATOR].New()
                    : this[ACCELERATOR].New(reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ, 0);
                if (this[ACCELERATOR].IsChanged)
                {
                    OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                    OnSensorUpdated?.Invoke(this[ACCELERATOR]);
                }
            });

            if (SensorSwitches.A.HasValue && (SensorSwitches.A.Value == 1 || SensorSwitches.A.Value == 3))
            {
                SensorSwitches.A = 0;
            }
        }

        public async void NewGyro(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await
                dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        this[GYROSCOPE] = reading == null
                            ? this[GYROSCOPE].New(0, 0, 0, 0)
                            : this[GYROSCOPE].New(reading.AngularVelocityX, reading.AngularVelocityY,
                                reading.AngularVelocityZ, 0);
                        if (this[GYROSCOPE].IsChanged)
                        {
                            OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            OnSensorUpdated?.Invoke(this[GYROSCOPE]);
                        }
                    });

            if (SensorSwitches.G.HasValue && (SensorSwitches.G.Value == 1 || SensorSwitches.G.Value == 3))
            {
                SensorSwitches.G = 0;
            }
        }

        public async void NewCom(Compass sender, CompassReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await
                dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        this[COMPASS] = reading == null
                            ? this[COMPASS].New(0, 0, 0, 0)
                            : this[COMPASS].New(reading.HeadingMagneticNorth, reading.HeadingTrueNorth ?? 0, 0, 0);
                        if (this[COMPASS].IsChanged)
                        {
                            OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            OnSensorUpdated?.Invoke(this[COMPASS]);
                        }
                    });

            if (SensorSwitches.M.HasValue && (SensorSwitches.M.Value == 1 || SensorSwitches.M.Value == 3))
            {
                SensorSwitches.M = 0;
            }
        }

        public async void NewLoc(Geolocator sender, PositionChangedEventArgs args)
        {
            var reading = args == null ? (sender == null ? null : (await sender.GetGeopositionAsync())) : args.Position;
            await
                dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        this[LOCATION] = reading == null
                            ? this[LOCATION].New(0, 0, 0, 0)
                            : this[LOCATION].New(reading.Coordinate.Point.Position.Latitude, reading.Coordinate.Point.Position.Longitude,
                                reading.Coordinate.Point.Position.Altitude, 0);
                        if (this[LOCATION].IsChanged)
                        {
                            OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            OnSensorUpdated?.Invoke(this[LOCATION]);
                        }
                    });

            if (SensorSwitches.L.HasValue && (SensorSwitches.L.Value == 1 || SensorSwitches.L.Value == 3))
            {
                SensorSwitches.L = 0;
            }
        }

        public async void NewQuan(OrientationSensor sender, OrientationSensorReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    this[QUANTIZATION] = reading == null
                        ? this[QUANTIZATION].New(0, 0, 0, 0)
                        : this[QUANTIZATION].New(reading.Quaternion.X, reading.Quaternion.Y, reading.Quaternion.Z,
                            reading.Quaternion.W);
                    if (this[QUANTIZATION].IsChanged)
                    {
                        OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                        OnSensorUpdated?.Invoke(this[QUANTIZATION]);
                    }
                });

            if (SensorSwitches.Q.HasValue && (SensorSwitches.Q.Value == 1 || SensorSwitches.Q.Value == 3))
            {
                SensorSwitches.Q = 0;
            }
        }

        private const uint baseMinimum = 100; //ms

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async void Start()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (SensorSwitches.A != null)
            {
                if (SensorSwitches.A.Value > 0)
                {
                    if (accelerometer == null)
                    {
                        accelerometer = Accelerometer.GetDefault();
                    }

                    if (accelerometer != null)
                    {
                        accelerometer.ReportInterval = Math.Max(Math.Max(baseMinimum, (uint) SensorSwitches.Interval), accelerometer.MinimumReportInterval);
                        if (SensorSwitches.A.Value != 1)
                        {
                            accelerometer.ReadingChanged += NewAcc;
                        }

                        this[ACCELERATOR].Id = SensorSwitches.Id;
                        this[ACCELERATOR].Delta = SensorSwitches.Delta;

                        if (SensorSwitches.A.Value != 3)
                        {
                            SensorSwitches.A = null;
                            NewAcc(accelerometer, null);
                        }
                        else
                        {
                            SensorSwitches.A = null;
                        }
                    }
                }
                else
                {
                    if (accelerometer != null)
                    {
                        accelerometer.ReadingChanged -= NewAcc;
                        NewAcc(null, null);
                    }

                    SensorSwitches.A = null;
                }
            }

            if (SensorSwitches.G != null)
            {
                if (SensorSwitches.G.Value > 0)
                {
                    if (gyrometer == null)
                    {
                        gyrometer = Gyrometer.GetDefault();
                    }

                    if (gyrometer != null)
                    {
                        gyrometer.ReportInterval = Math.Max(Math.Max(baseMinimum, (uint)SensorSwitches.Interval), gyrometer.MinimumReportInterval);
                        if (SensorSwitches.G.Value != 1)
                        {
                            gyrometer.ReadingChanged += NewGyro;
                        }

                        this[GYROSCOPE].Id = SensorSwitches.Id;
                        this[GYROSCOPE].Delta = SensorSwitches.Delta;

                        if (SensorSwitches.G.Value != 3)
                        {
                            SensorSwitches.G = null;
                            NewGyro(gyrometer, null);
                        }
                        else
                        {
                            SensorSwitches.G = null;
                        }
                    }
                }
                else
                {
                    if (gyrometer != null)
                    {
                        gyrometer.ReadingChanged -= NewGyro;
                        NewGyro(null, null);
                    }

                    SensorSwitches.G = null;
                }
            }

            if (SensorSwitches.M != null)
            {
                if (SensorSwitches.M.Value > 0)
                {
                    if (compass == null)
                    {
                        compass = Compass.GetDefault();
                    }

                    if (compass != null)
                    {
                        compass.ReportInterval = Math.Max(Math.Max(baseMinimum, (uint)SensorSwitches.Interval), compass.MinimumReportInterval);

                        if (SensorSwitches.M.Value != 1)
                        {
                            compass.ReadingChanged += NewCom;
                        }

                        this[COMPASS].Id = SensorSwitches.Id;
                        this[COMPASS].Delta = SensorSwitches.Delta;

                        if (SensorSwitches.M.Value != 3)
                        {
                            SensorSwitches.M = null;
                            NewCom(compass, null);
                        }
                        else
                        {
                            SensorSwitches.M = null;
                        }
                    }
                }
                else
                {
                    if (compass != null)
                    {
                        compass.ReadingChanged -= NewCom;
                        NewCom(null, null);
                    }

                    SensorSwitches.M = null;
                }
            }

            if (SensorSwitches.L != null)
            {
                if (SensorSwitches.L.Value > 0)
                {
                    if (geolocator == null)
                    {
                        geolocator = new Geolocator();
                    }

                    if (geolocator != null)
                    {
                        geolocator.ReportInterval = 30*60*1000;

                        this[LOCATION].Id = SensorSwitches.Id;
                        this[LOCATION].Delta = SensorSwitches.Delta;

                        if (SensorSwitches.L.Value != 1)
                        {
                            geolocator.PositionChanged += NewLoc;
                        }

                        if (SensorSwitches.L.Value != 3)
                        {
                            SensorSwitches.L = null;
                            NewLoc(geolocator, null);
                        }
                        else
                        {
                            SensorSwitches.L = null;
                        }
                    }
                }
                else
                {
                    if (geolocator != null)
                    {
                        geolocator.PositionChanged -= NewLoc;
                        NewLoc(null, null);
                    }

                    SensorSwitches.L = null;
                }
            }

            if (SensorSwitches.Q != null)
            {
                if (SensorSwitches.Q.Value > 0)
                {
                    if (orientation == null)
                    {
                        orientation = OrientationSensor.GetDefault();
                    }

                    if (orientation != null)
                    {
                        this[QUANTIZATION].Id = SensorSwitches.Id;
                        this[QUANTIZATION].Delta = SensorSwitches.Delta;

                        orientation.ReportInterval = Math.Max(Math.Max(baseMinimum, (uint)SensorSwitches.Interval), orientation.MinimumReportInterval);
                        if (SensorSwitches.Q.Value != 1)
                        {
                            orientation.ReadingChanged += NewQuan;
                        }

                        if (SensorSwitches.Q.Value != 3)
                        {
                            SensorSwitches.Q = null;
                            NewQuan(orientation, null);
                        }
                        else
                        {
                            SensorSwitches.Q = null;
                        }
                    }
                }
                else
                {
                    if (orientation != null)
                    {
                        orientation.ReadingChanged -= NewQuan;
                        NewQuan(null, null);
                    }

                    SensorSwitches.Q = null;
                }
            }

            if (SensorSwitches.P != null)
            {
                if (SensorSwitches.P.Value > 0)
                {
                    if (lightSensor == null)
                    {
                        lightSensor = LightSensor.GetDefault();
                    }

                    if (lightSensor != null)
                    {
                        this[LIGHTSENSOR].Id = SensorSwitches.Id;
                        this[LIGHTSENSOR].Delta = SensorSwitches.Delta;

                        lightSensor.ReportInterval = Math.Max(Math.Max(baseMinimum, (uint)SensorSwitches.Interval), lightSensor.MinimumReportInterval);
                        if (SensorSwitches.P.Value != 1)
                        {
                            lightSensor.ReadingChanged += NewLight;
                        }

                        if (SensorSwitches.P.Value != 3)
                        {
                            SensorSwitches.P = null;
                            NewLight(lightSensor, null);
                        }
                        else
                        {
                            SensorSwitches.P = null;
                        }
                    }
                }
                else
                {
                    if (lightSensor != null)
                    {
                        lightSensor.ReadingChanged -= NewLight;
                        NewQuan(null, null);
                    }

                    SensorSwitches.P = null;
                }
            }
        }
    }
}