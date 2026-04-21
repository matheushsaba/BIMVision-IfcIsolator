using Newtonsoft.Json;
using System.IO.Pipes;
using System.Windows.Forms;

namespace CoreLayer
{
    public partial class MainWindow
    {
        public static void Main()
        {
            InitializeApiListener();
            SendCommand(new Message { Type = MessageType.CORE_STATUS_READY });
        }
        static void InitializeApiListener()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    using var server = new NamedPipeServerStream("Api_To_Core_Pipe", PipeDirection.In, 1, PipeTransmissionMode.Message);

                    server.WaitForConnection();

                    using var reader = new StreamReader(server);
                    var msgString = reader.ReadLine();

                    if (msgString != null)
                    {
                        var message = JsonConvert.DeserializeObject<Message>(msgString);

                        switch (message.Type)
                        {
                            case MessageType.API_STATUS_READY:
                                {
                                    if (MessageBox.Show("CoreLayer: API_STATUS_READY command received.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information) == DialogResult.OK)
                                        SendCommand(new Message { Type = MessageType.APP_READY });
                                }
                                break;
                            default: break;
                        }
                    }
                }
            });
        }
        static void SendCommand(Message command)
        {
            using var client = new NamedPipeClientStream(".", "Core_To_Api_Pipe", PipeDirection.Out);

            client.Connect(2000);

            using var writer = new StreamWriter(client);

            writer.AutoFlush = true;
            writer.WriteLine(JsonConvert.SerializeObject(command));
        }
    }
}