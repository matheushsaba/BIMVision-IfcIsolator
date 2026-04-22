namespace ApiLayer
{
    public class Message
    {
        public MessageType Type { get; set; }
        public Argument[] Arguments { get; set; }
    }
    public enum MessageType
    {
        ISOLATE_SINGLE_IFC_REQUEST,
        ISOLATE_SINGLE_IFC_COMPLETED,
        ISOLATE_SINGLE_IFC_FAILED,
        ISOLATE_SINGLE_IFC_CANCELED,
    }
    public class Argument
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
