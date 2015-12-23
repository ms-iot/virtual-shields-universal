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
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;

    using Windows.System.Profile;

    /// <summary>
    /// TelemetryInitializer to improve Device Family and Device Type telemetry for ApplicationInsights
    /// </summary>
    public class UwpDeviceTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Properties["device.family"] = AnalyticsInfo.VersionInfo.DeviceFamily;

            // AppInsights *always* sets Device.Type to "Phone" for a UWP application.  Override with
            // a more useful value.
            switch (telemetry.Context.Properties["device.family"])
            {
                case "Windows.Desktop":
                    telemetry.Context.Device.Type = "PC";
                    break;
                case "Windows.Mobile":
                    telemetry.Context.Device.Type = "Phone";
                    break;
                case "Windows.IoT":
                    telemetry.Context.Device.Type = "IoT";
                    break;
                default:
                    telemetry.Context.Device.Type = "Other";
                    break;
            }
        }
    }
}