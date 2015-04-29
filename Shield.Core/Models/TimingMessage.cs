namespace Shield.Core.Models
{
    [Service("VIBRATE")]
    [Service("MICROPHONE")]
    [Service("PLAY")]
    public class TimingMessage : MessageBase, IAddressable
    {
        public int Ms { get; set; }

        public string Url { get; set; }
        public bool? Autoplay { get; set; }
        public bool? Keep { get; set; }
    }
}