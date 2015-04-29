namespace Shield.Core.Models
{
    public class ScreenResultMessage : ResultMessage
    {
        public ScreenResultMessage() : base()
        {
            Type = 'S';
        }

        public ScreenResultMessage(MessageBase message) : base(message)
        {
            Type = message.Type ?? 'S';
        }

        public double X { get; set; }
        public double Y { get; set; }
        public string Area { get; set; }
        public string Tag { get; set; }
    }
}