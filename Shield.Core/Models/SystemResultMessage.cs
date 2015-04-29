namespace Shield.Core.Models
{
    public class SystemResultMessage : ResultMessage
    {
        public SystemResultMessage(string result)
        {
            this.Service = "SYSTEM";
            this.Type = '!';
            this.Result = result;
        }
    }
}