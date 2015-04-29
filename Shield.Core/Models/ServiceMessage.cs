namespace Shield.Core.Models
{
    [Service("SERVICE")]
    public class ServiceMessage : MessageBase
    {
        public string Action { get; set; }

        public ServiceMessage()
        {
            this.Known = true;
        }
    }
}