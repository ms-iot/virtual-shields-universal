namespace Shield.Core.Models
{
    public class ResultMessage
    {
        public string Service { get; set; }
        public int? Id { get; set; }
        public char? Type { get; set; }
        public int? ResultId { get; set; }
        public string Result { get; set; }
        public string Action { get; set; }

        public ResultMessage()
        {
        }

        public ResultMessage(MessageBase message)
        {
            this.Service = message.Service;
            this.Id = message.Id;
            this.Type = message.Type;
        }
    }
}