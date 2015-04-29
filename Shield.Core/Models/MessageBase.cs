namespace Shield.Core.Models
{
    public class MessageBase
    {
        public string Service { get; set; }
        public bool Known { get; set; }
        public int Id { get; set; }

        public char? Type { get; set; }
        public string Action { get; set; }

        public string _Source { get; set; }
    }
}