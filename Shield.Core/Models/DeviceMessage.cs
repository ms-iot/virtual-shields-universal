namespace Shield.Core.Models
{
    [Service("DEVICE")]
    public class DeviceMessage : MessageBase
    {
        public string Message { get; set; }
        public string Verb { get; set; }
        public string Key { get; set; }
    }
}