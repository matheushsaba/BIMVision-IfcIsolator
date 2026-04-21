namespace CoreLayer
{
    public class Message
    {
        public MessageType Type { get; set; }
        public Argument[] Arguments { get; set; }
    }
    public enum MessageType
    {
        CORE_STATUS_READY,
        API_STATUS_READY,
        APP_READY,
    }
    public class Argument
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
