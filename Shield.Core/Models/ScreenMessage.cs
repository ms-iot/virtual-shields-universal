namespace Shield.Core.Models
{
    [Service("LCDT")]
    [Service("LCDG")]
    [Service("LOG")]
    public class ScreenMessage : MessageBase
    {
        public string Message { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? X2 { get; set; }
        public int? Y2 { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string Tag { get; set; }
        public string Path { get; set; }
        public int? Pid { get; set; }
        public string ARGB { get; set; }
        public string Foreground { get; set; }
        public string HorizontalAlignment { get; set; }
        public int? Value { get; set; }
        public string FlowDirection { get; set; }
        public int? Size { get; set; }
        public bool? Multi { get; set; }
    }
}