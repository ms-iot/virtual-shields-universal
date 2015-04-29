namespace Shield.Core.Models
{
    public class DeviceResultMessage : ResultMessage
    {
        public DeviceResultMessage() : base()
        {
            Type = 'K';
        }

        public DeviceResultMessage(MessageBase message) : base(message)
        {
            Type = message.Type ?? 'K';
        }

        public double ResultD { get; set; }
        public double Offset { get; set; }

    }
}