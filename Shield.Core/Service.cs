namespace Shield.Core
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]

    public class Service : System.Attribute
    {
        public string Id { get; private set; }

        public Service(string id)
        {
            this.Id = id;
        }
    }
}