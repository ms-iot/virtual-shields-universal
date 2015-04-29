namespace Shield.Core.Models
{
    [Service("NOTIFY")]
    public class NotifyMessage : MessageBase
    {
        public string Message { get; set; }
        public string Tag { get; set; }
        public string Audio { get; set; }
        public string Image { get; set; }
    }
}