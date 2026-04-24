namespace CoreLayer
{
    public class Message
    {
        public MessageType Type { get; set; }
        public Argument[] Arguments { get; set; }
    }
    public enum MessageType
    {
        APP_READY,
        SHOW_FIND_BY_GUID_WINDOW,
        FIND_BY_GUID_REQUEST,
        FIND_BY_GUID_COMPLETED,
    }
    public class Argument
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
