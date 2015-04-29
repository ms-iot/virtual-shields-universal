namespace Shield.Core.Models
{
    public interface IAddressable
    {
        string Service { get; set; }
        string Url { get; set; } 
    }
}