using System.Collections.Generic;

namespace Shield.Core.Models
{
    [Service("EMAIL")]
    public class EmailMessage : MessageBase
    {
        public List<KeyValuePair<string, string>> Headers { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Attachment { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }
    }
}