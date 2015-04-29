namespace Shield.Core.Models
{
    [Service("SPEECH")]
    public class SpeechMessage : MessageBase
    {
        public string Message { get; set; }
    }
}