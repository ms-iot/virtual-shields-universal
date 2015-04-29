namespace Shield.Core.Models
{
    [Service("RECOGNIZE")]
    public class SpeechRecognitionMessage : MessageBase
    {
        public string Message { get; set; }
        public bool UI { get; set; }
        public int? Confidence { get; set; }
        public long? Ms { get; set; }
    }
}