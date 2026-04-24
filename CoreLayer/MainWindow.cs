using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoreLayer
{
    public sealed class MainWindow : ApplicationContext
    {
        readonly FindByGuidWindow _findByGuidWindow;

        public MainWindow()
        {
            _findByGuidWindow = new FindByGuidWindow(SendFindByGuidRequest);
            _ = _findByGuidWindow.Handle;

            InitializeApiListener();
            SendReadyMessage();
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new MainWindow());
        }

        void InitializeApiListener()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    using (var server = new NamedPipeServerStream("Api_To_Core_Pipe", PipeDirection.In, 1, PipeTransmissionMode.Message))
                    {
                        server.WaitForConnection();

                        using (var reader = new StreamReader(server))
                        {
                            var msgString = reader.ReadLine();
                            if (msgString == null)
                                continue;

                            var message = DeserializeMessage(msgString);

                            switch (message.Type)
                            {
                                case MessageType.SHOW_FIND_BY_GUID_WINDOW:
                                    ShowFindByGuidWindow(ParseWindowHandle(GetArgument(message, "ViewerHandle")));
                                    break;
                                case MessageType.FIND_BY_GUID_COMPLETED:
                                    HandleFindByGuidCompleted(message);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            });
        }

        void ShowFindByGuidWindow(IntPtr ownerHandle)
        {
            _findByGuidWindow.BeginInvoke((Action)(() => _findByGuidWindow.ShowOwnedWindow(ownerHandle)));
        }

        void HandleFindByGuidCompleted(Message message)
        {
            var countValue = GetArgument(message, "MatchCount");
            var matchCount = 0;

            if (!string.IsNullOrWhiteSpace(countValue))
            {
                int.TryParse(countValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out matchCount);
            }

            _findByGuidWindow.BeginInvoke((Action)(() => _findByGuidWindow.HandleSearchCompleted(matchCount)));
        }

        void SendReadyMessage()
        {
            Task.Run(() =>
            {
                try
                {
                    SendCommand(new Message { Type = MessageType.APP_READY });
                }
                catch (Exception)
                {
                }
            });
        }

        void SendFindByGuidRequest(string guid)
        {
            try
            {
                SendCommand(new Message
                {
                    Type = MessageType.FIND_BY_GUID_REQUEST,
                    Arguments = new[]
                    {
                        new Argument { Name = "Guid", Value = guid },
                    },
                });
            }
            catch (TimeoutException)
            {
                MessageBox.Show(
                    _findByGuidWindow,
                    "BIM Vision did not respond in time. Please try again.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    _findByGuidWindow,
                    "Unable to send the GUID search request.\n\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        static void SendCommand(Message command)
        {
            var serializedCommand = SerializeMessage(command);
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (true)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", "Core_To_Api_Pipe", PipeDirection.Out))
                    {
                        client.Connect(500);

                        using (var writer = new StreamWriter(client))
                        {
                            writer.AutoFlush = true;
                            writer.WriteLine(serializedCommand);
                            return;
                        }
                    }
                }
                catch (TimeoutException) when (DateTime.UtcNow < deadline)
                {
                    Task.Delay(250).Wait();
                }
            }
        }

        static string GetArgument(Message message, string name)
        {
            if (message.Arguments == null)
                return null;

            foreach (var argument in message.Arguments)
            {
                if (string.Equals(argument.Name, name, StringComparison.OrdinalIgnoreCase))
                    return argument.Value;
            }

            return null;
        }

        static IntPtr ParseWindowHandle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return IntPtr.Zero;

            long handleValue;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out handleValue)
                ? new IntPtr(handleValue)
                : IntPtr.Zero;
        }

        static string SerializeMessage(Message message)
        {
            var parts = new List<string> { message.Type.ToString() };

            if (message.Arguments != null)
            {
                foreach (var argument in message.Arguments)
                {
                    parts.Add(Encode(argument.Name) + ":" + Encode(argument.Value));
                }
            }

            return string.Join("|", parts);
        }

        static Message DeserializeMessage(string value)
        {
            var parts = value.Split('|');
            var arguments = new List<Argument>();

            for (var i = 1; i < parts.Length; i++)
            {
                var separatorIndex = parts[i].IndexOf(':');
                if (separatorIndex <= 0)
                    continue;

                arguments.Add(new Argument
                {
                    Name = Decode(parts[i].Substring(0, separatorIndex)),
                    Value = Decode(parts[i].Substring(separatorIndex + 1)),
                });
            }

            return new Message
            {
                Type = (MessageType)Enum.Parse(typeof(MessageType), parts[0]),
                Arguments = arguments.ToArray(),
            };
        }

        static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
