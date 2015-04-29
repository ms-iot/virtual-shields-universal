namespace Shield.Core.Models
{
    public class SpeechResultMessage : ResultMessage
    {
        private const char ServiceType = 'R';

        public SpeechResultMessage() : base()
        {
            Type = ServiceType;
        }

        public SpeechResultMessage(MessageBase message) : base(message)
        {
            Type = message.Type ?? ServiceType;
        }

        public int Value { get; set; }
    }
}