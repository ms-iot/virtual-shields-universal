namespace Shield.Core.Models
{
    [Service("SMS")]
    public class SmsMessage : MessageBase
    {
        public string To { get; set; }
        public string Message { get; set; }
        public string Attachment { get; set; }

        public SmsMessage()
        {
            this.Known = true;
        }
    }
}