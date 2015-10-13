
namespace Shield
{
    using Microsoft.ApplicationInsights.Extensibility;

    /// <summary>
    /// TelemetryInitializer to improve Device Family and Device Type telemetry for ApplicationInsights
    /// </summary>
    public class UwpDeviceTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry)
        {
            telemetry.Context.Properties["device.family"] = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily;

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