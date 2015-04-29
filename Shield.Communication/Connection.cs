namespace Shield.Communication
{
    public class Connection
    {
        public string DisplayName { get; set; }
        public object Source { get; set; }

        public Connection(string displayName, object source)
        {
            this.DisplayName = displayName;
            this.Source = source;
        }
    }
}