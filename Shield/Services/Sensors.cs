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
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;

    using Shield.Core;

    using Windows.Devices.Geolocation;
    using Windows.Devices.Sensors;
    using Windows.UI.Core;
    using Windows.UI.Xaml;

    public class DataItem : DependencyObject, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", 
            typeof(double), 
            typeof(DataItem), 
            new PropertyMetadata(0d, OnValueChanged));

        public string Name { get; set; }

        public double Value
        {
            get
            {
                return (double)this.GetValue(ValueProperty);
            }

            set
            {
                this.SetValue(ValueProperty, value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs a)
        {
            var di = d as DataItem;
            di.NotifyPropertyChanged("Value");
        }

        private void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }

    public class DataItems : ObservableCollection<DataItem>
    {
        public void Refresh()
        {
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public class XYZ : DependencyObject
    {
        public bool IsChanged;

        public XYZ()
        {
        }

        public XYZ(object source, string name, string tag, string[] names)
        {
            this.Source = source;
            this.Name = name;
            this.Tag = tag;

            this.Data = new DataItems();
            foreach (var item in names)
            {
                var newitem = new DataItem { Name = item, Value = 0 };
                newitem.PropertyChanged += this.SubPropertyChanged;

                this.Data.Add(newitem);
            }
        }

        // public double X { get; set; }

        // public double Y { get; set; }
        // public double Z { get; set; }
        // public double W { get; set; }
        public int Id { get; set; }

        public object Source { get; set; }

        public string Name { get; set; }

        public string Tag { get; set; }

        public double Delta { get; set; }

        public DataItems Data { get; set; }

        private bool IsWithinDelta(double a, double b)
        {
            return Math.Abs(b - a) < this.Delta;
        }

        public XYZ New(double x, double y, double z, double w)
        {
            var item = this; // ew XYZ() { Source = Source, Data = Data, Name = Name, Tag = Tag };

            if (this.Delta > 0 && this.IsWithinDelta(x, item.Data[0].Value) && this.IsWithinDelta(y, item.Data[1].Value)
                && this.IsWithinDelta(z, item.Data[2].Value)
                && (item.Data.Count() < 4 || this.IsWithinDelta(w, item.Data[3].Value)))
            {
                this.IsChanged = false;
                return this; // no change
            }

            this.IsChanged = true;

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
            this.IsChanged = true;
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

        private const uint baseMinimum = 100; // ms

        private readonly CoreDispatcher dispatcher;

        private Accelerometer accelerometer;

        private Compass compass;

        private Geolocator geolocator;

        private Gyrometer gyrometer;

        private LightSensor lightSensor;

        private OrientationSensor orientation;

        public Sensors()
        {
            this.Add(new XYZ(this.accelerometer, "Accelerometer", "A", new[] { "X", "Y", "Z" }));
            this.Add(new XYZ(this.gyrometer, "Gyroscope", "G", new[] { "X", "Y", "Z" }));
            this.Add(new XYZ(this.compass, "Compass", "M", new[] { "Mag", "True" }));
            this.Add(new XYZ(this.geolocator, "Location", "L", new[] { "Lat", "Lon", "Alt" }));
            this.Add(new XYZ(this.orientation, "Orientation", "Q", new[] { "X", "Y", "Z", "W" }));
            this.Add(new XYZ(this.lightSensor, "LightSensor", "P", new[] { "Lux" }));
        }

        public Sensors(CoreDispatcher dispatcher)
            : this()
        {
            this.dispatcher = dispatcher;
            this.SensorSwitches = new SensorSwitches();
        }

        public Sensors ItemsList => this;

        public SensorSwitches SensorSwitches { get; set; }

        public bool IsSending { get; set; }

        public event SensorUpdated OnSensorUpdated;

        public async void NewLight(LightSensor sender, LightSensorReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this[LIGHTSENSOR] = reading == null
                                                ? this[LIGHTSENSOR].New()
                                                : this[LIGHTSENSOR].New(reading.IlluminanceInLux, 0, 0, 0);
                        if (this[LIGHTSENSOR].IsChanged)
                        {
                            this.OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            this.OnSensorUpdated?.Invoke(this[LIGHTSENSOR]);
                        }
                    });

            if (this.SensorSwitches.P.HasValue && (this.SensorSwitches.P.Value == 1 || this.SensorSwitches.P.Value == 3))
            {
                this.SensorSwitches.P = 0;
            }
        }

        public async void NewAcc(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this[ACCELERATOR] = reading == null
                                                ? this[ACCELERATOR].New()
                                                : this[ACCELERATOR].New(
                                                    reading.AccelerationX, 
                                                    reading.AccelerationY, 
                                                    reading.AccelerationZ, 
                                                    0);
                        if (this[ACCELERATOR].IsChanged)
                        {
                            this.OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            this.OnSensorUpdated?.Invoke(this[ACCELERATOR]);
                        }
                    });

            if (this.SensorSwitches.A.HasValue && (this.SensorSwitches.A.Value == 1 || this.SensorSwitches.A.Value == 3))
            {
                this.SensorSwitches.A = 0;
            }
        }

        public async void NewGyro(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this[GYROSCOPE] = reading == null
                                              ? this[GYROSCOPE].New(0, 0, 0, 0)
                                              : this[GYROSCOPE].New(
                                                  reading.AngularVelocityX, 
                                                  reading.AngularVelocityY, 
                                                  reading.AngularVelocityZ, 
                                                  0);
                        if (this[GYROSCOPE].IsChanged)
                        {
                            this.OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            this.OnSensorUpdated?.Invoke(this[GYROSCOPE]);
                        }
                    });

            if (this.SensorSwitches.G.HasValue && (this.SensorSwitches.G.Value == 1 || this.SensorSwitches.G.Value == 3))
            {
                this.SensorSwitches.G = 0;
            }
        }

        public async void NewCom(Compass sender, CompassReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this[COMPASS] = reading == null
                                            ? this[COMPASS].New(0, 0, 0, 0)
                                            : this[COMPASS].New(
                                                reading.HeadingMagneticNorth, 
                                                reading.HeadingTrueNorth ?? 0, 
                                                0, 
                                                0);
                        if (this[COMPASS].IsChanged)
                        {
                            this.OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            this.OnSensorUpdated?.Invoke(this[COMPASS]);
                        }
                    });

            if (this.SensorSwitches.M.HasValue && (this.SensorSwitches.M.Value == 1 || this.SensorSwitches.M.Value == 3))
            {
                this.SensorSwitches.M = 0;
            }
        }

        public async void NewLoc(Geolocator sender, PositionChangedEventArgs args)
        {
            Geoposition reading = null;

            try
            {
                reading = args == null ? (sender == null ? null : (await sender.GetGeopositionAsync())) : args.Position;
            }
            catch (UnauthorizedAccessException uae)
            {
                throw new UnsupportedSensorException("Geolocator not enabled : " + uae.Message);
            }

            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this[LOCATION] = reading == null
                                             ? this[LOCATION].New(0, 0, 0, 0)
                                             : this[LOCATION].New(
                                                 reading.Coordinate.Point.Position.Latitude, 
                                                 reading.Coordinate.Point.Position.Longitude, 
                                                 reading.Coordinate.Point.Position.Altitude, 
                                                 0);
                        if (this[LOCATION].IsChanged)
                        {
                            this.OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            this.OnSensorUpdated?.Invoke(this[LOCATION]);
                        }
                    });

            if (this.SensorSwitches.L.HasValue && (this.SensorSwitches.L.Value == 1 || this.SensorSwitches.L.Value == 3))
            {
                this.SensorSwitches.L = 0;
            }
        }

        public async void NewQuan(OrientationSensor sender, OrientationSensorReadingChangedEventArgs args)
        {
            var reading = args == null ? sender?.GetCurrentReading() : args.Reading;
            await this.dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, 
                () =>
                    {
                        this[QUANTIZATION] = reading == null
                                                 ? this[QUANTIZATION].New(0, 0, 0, 0)
                                                 : this[QUANTIZATION].New(
                                                     reading.Quaternion.X, 
                                                     reading.Quaternion.Y, 
                                                     reading.Quaternion.Z, 
                                                     reading.Quaternion.W);
                        if (this[QUANTIZATION].IsChanged)
                        {
                            this.OnPropertyChanged(new PropertyChangedEventArgs("ItemsList"));
                            this.OnSensorUpdated?.Invoke(this[QUANTIZATION]);
                        }
                    });

            if (this.SensorSwitches.Q.HasValue && (this.SensorSwitches.Q.Value == 1 || this.SensorSwitches.Q.Value == 3))
            {
                this.SensorSwitches.Q = 0;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async void Start()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                this.ToggleAccelerometer();
            }
            catch (Exception e)
            {
                throw new UnsupportedSensorException("Accelerometer error: " + e.Message);
            }

            try
            {
                this.ToggleGyrometer();
            }
            catch (Exception e)
            {
                throw new UnsupportedSensorException("Gyrometer error: " + e.Message);
            }

            try
            {
                this.ToggleCompass();
            }
            catch (Exception e)
            {
                throw new UnsupportedSensorException("Compass error: " + e.Message);
            }

            try
            {
                this.ToggleGeolocator();
            }
            catch (Exception e)
            {
                throw new UnsupportedSensorException("Geolocator error: " + e.Message);
            }

            try
            {
                this.ToggleOrientation();
            }
            catch (Exception e)
            {
                throw new UnsupportedSensorException("Orientation error: " + e.Message);
            }

            try
            {
                this.ToggleLightSensor();
            }
            catch (Exception e)
            {
                throw new UnsupportedSensorException("LightSensor error: " + e.Message);
            }
        }

        private void ToggleLightSensor()
        {
            if (this.SensorSwitches.P != null)
            {
                if (this.SensorSwitches.P.Value > 0)
                {
                    if (this.lightSensor == null)
                    {
                        this.lightSensor = LightSensor.GetDefault();
                    }

                    if (this.lightSensor != null)
                    {
                        this[LIGHTSENSOR].Id = this.SensorSwitches.Id;
                        this[LIGHTSENSOR].Delta = this.SensorSwitches.Delta;

                        this.lightSensor.ReportInterval =
                            Math.Max(
                                Math.Max(baseMinimum, (uint)this.SensorSwitches.Interval), 
                                this.lightSensor.MinimumReportInterval);
                        if (this.SensorSwitches.P.Value != 1)
                        {
                            this.lightSensor.ReadingChanged += this.NewLight;
                        }

                        if (this.SensorSwitches.P.Value != 3)
                        {
                            this.SensorSwitches.P = null;
                            this.NewLight(this.lightSensor, null);
                        }
                        else
                        {
                            this.SensorSwitches.P = null;
                        }
                    }
                }
                else
                {
                    if (this.lightSensor != null)
                    {
                        this.lightSensor.ReadingChanged -= this.NewLight;
                        this.NewQuan(null, null);
                    }

                    this.SensorSwitches.P = null;
                }
            }
        }

        private void ToggleOrientation()
        {
            if (this.SensorSwitches.Q != null)
            {
                if (this.SensorSwitches.Q.Value > 0)
                {
                    if (this.orientation == null)
                    {
                        this.orientation = OrientationSensor.GetDefault();
                    }

                    if (this.orientation != null)
                    {
                        this[QUANTIZATION].Id = this.SensorSwitches.Id;
                        this[QUANTIZATION].Delta = this.SensorSwitches.Delta;

                        this.orientation.ReportInterval =
                            Math.Max(
                                Math.Max(baseMinimum, (uint)this.SensorSwitches.Interval), 
                                this.orientation.MinimumReportInterval);
                        if (this.SensorSwitches.Q.Value != 1)
                        {
                            this.orientation.ReadingChanged += this.NewQuan;
                        }

                        if (this.SensorSwitches.Q.Value != 3)
                        {
                            this.SensorSwitches.Q = null;
                            this.NewQuan(this.orientation, null);
                        }
                        else
                        {
                            this.SensorSwitches.Q = null;
                        }
                    }
                }
                else
                {
                    if (this.orientation != null)
                    {
                        this.orientation.ReadingChanged -= this.NewQuan;
                        this.NewQuan(null, null);
                    }

                    this.SensorSwitches.Q = null;
                }
            }
        }

        private void ToggleGeolocator()
        {
            if (this.SensorSwitches.L != null)
            {
                if (this.SensorSwitches.L.Value > 0)
                {
                    if (this.geolocator == null)
                    {
                        this.geolocator = new Geolocator();
                    }

                    if (this.geolocator != null)
                    {
                        this.geolocator.ReportInterval = 30 * 60 * 1000;

                        this[LOCATION].Id = this.SensorSwitches.Id;
                        this[LOCATION].Delta = this.SensorSwitches.Delta;

                        if (this.SensorSwitches.L.Value != 1)
                        {
                            this.geolocator.PositionChanged += this.NewLoc;
                        }

                        if (this.SensorSwitches.L.Value != 3)
                        {
                            this.SensorSwitches.L = null;
                            try
                            {
                                this.NewLoc(this.geolocator, null);
                            }
                            catch (UnsupportedSensorException use)
                            {
                                // record
                                this.SensorSwitches.L = null;
                            }
                        }
                        else
                        {
                            this.SensorSwitches.L = null;
                        }
                    }
                }
                else
                {
                    if (this.geolocator != null)
                    {
                        this.geolocator.PositionChanged -= this.NewLoc;
                        this.NewLoc(null, null);
                    }

                    this.SensorSwitches.L = null;
                }
            }
        }

        private void ToggleCompass()
        {
            if (this.SensorSwitches.M != null)
            {
                if (this.SensorSwitches.M.Value > 0)
                {
                    if (this.compass == null)
                    {
                        this.compass = Compass.GetDefault();
                    }

                    if (this.compass != null)
                    {
                        this.compass.ReportInterval = Math.Max(
                            Math.Max(baseMinimum, (uint)this.SensorSwitches.Interval), 
                            this.compass.MinimumReportInterval);

                        if (this.SensorSwitches.M.Value != 1)
                        {
                            this.compass.ReadingChanged += this.NewCom;
                        }

                        this[COMPASS].Id = this.SensorSwitches.Id;
                        this[COMPASS].Delta = this.SensorSwitches.Delta;

                        if (this.SensorSwitches.M.Value != 3)
                        {
                            this.SensorSwitches.M = null;
                            this.NewCom(this.compass, null);
                        }
                        else
                        {
                            this.SensorSwitches.M = null;
                        }
                    }
                }
                else
                {
                    if (this.compass != null)
                    {
                        this.compass.ReadingChanged -= this.NewCom;
                        this.NewCom(null, null);
                    }

                    this.SensorSwitches.M = null;
                }
            }
        }

        private void ToggleGyrometer()
        {
            if (this.SensorSwitches.G != null)
            {
                if (this.SensorSwitches.G.Value > 0)
                {
                    if (this.gyrometer == null)
                    {
                        this.gyrometer = Gyrometer.GetDefault();
                    }

                    if (this.gyrometer != null)
                    {
                        this.gyrometer.ReportInterval =
                            Math.Max(
                                Math.Max(baseMinimum, (uint)this.SensorSwitches.Interval), 
                                this.gyrometer.MinimumReportInterval);
                        if (this.SensorSwitches.G.Value != 1)
                        {
                            this.gyrometer.ReadingChanged += this.NewGyro;
                        }

                        this[GYROSCOPE].Id = this.SensorSwitches.Id;
                        this[GYROSCOPE].Delta = this.SensorSwitches.Delta;

                        if (this.SensorSwitches.G.Value != 3)
                        {
                            this.SensorSwitches.G = null;
                            this.NewGyro(this.gyrometer, null);
                        }
                        else
                        {
                            this.SensorSwitches.G = null;
                        }
                    }
                }
                else
                {
                    if (this.gyrometer != null)
                    {
                        this.gyrometer.ReadingChanged -= this.NewGyro;
                        this.NewGyro(null, null);
                    }

                    this.SensorSwitches.G = null;
                }
            }
        }

        private void ToggleAccelerometer()
        {
            if (this.SensorSwitches.A != null)
            {
                if (this.SensorSwitches.A.Value > 0)
                {
                    if (this.accelerometer == null)
                    {
                        this.accelerometer = Accelerometer.GetDefault();
                    }

                    if (this.accelerometer != null)
                    {
                        this.accelerometer.ReportInterval =
                            Math.Max(
                                Math.Max(baseMinimum, (uint)this.SensorSwitches.Interval), 
                                this.accelerometer.MinimumReportInterval);
                        if (this.SensorSwitches.A.Value != 1)
                        {
                            this.accelerometer.ReadingChanged += this.NewAcc;
                        }

                        this[ACCELERATOR].Id = this.SensorSwitches.Id;
                        this[ACCELERATOR].Delta = this.SensorSwitches.Delta;

                        if (this.SensorSwitches.A.Value != 3)
                        {
                            this.SensorSwitches.A = null;
                            this.NewAcc(this.accelerometer, null);
                        }
                        else
                        {
                            this.SensorSwitches.A = null;
                        }
                    }
                }
                else
                {
                    if (this.accelerometer != null)
                    {
                        this.accelerometer.ReadingChanged -= this.NewAcc;
                        this.NewAcc(null, null);
                    }

                    this.SensorSwitches.A = null;
                }
            }
        }
    }
}