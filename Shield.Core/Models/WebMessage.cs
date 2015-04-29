using System.Collections.Generic;

namespace Shield.Core.Models
{
    [Service("WEB")]
    public class WebMessage : MessageBase, IAddressable
    {
        public List<KeyValuePair<string, string>> Headers { get; set; }
        public string Url { get; set; }

        public string Data { get; set; }
        public string Parse { get; set; }
        public int Len { get; set; }
    }
}