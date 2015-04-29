namespace Shield.Core.Models
{
    [Service("CAMERA")]
    public class CameraMessage : MessageBase, IAddressable
    {
        public string Url { get; set; }
        public string Message { get; set; }
        public bool? Keep { get; set; }
    }
}