using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using IfcIsolator;

namespace CoreLayer
{
    public partial class MainWindow
    {
        static string _lastSelectedFolder;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ListenForApiCommands();
        }

        static void ListenForApiCommands()
        {
            while (true)
            {
                using var server = new NamedPipeServerStream("Api_To_Core_Pipe", PipeDirection.In, 1, PipeTransmissionMode.Message);

                server.WaitForConnection();

                using var reader = new StreamReader(server);
                var msgString = reader.ReadLine();

                if (msgString == null)
                    continue;

                var message = DeserializeMessage(msgString);

                switch (message.Type)
                {
                    case MessageType.ISOLATE_SINGLE_IFC_REQUEST:
                        IsolateSingleIfc(message);
                        break;
                    default:
                        break;
                }
            }
        }

        static void IsolateSingleIfc(Message message)
        {
            var sourceFilePath = GetArgument(message, "SourceFilePath");
            var entityLabelsArgument = GetArgument(message, "EntityLabels");

            if (string.IsNullOrWhiteSpace(sourceFilePath) || string.IsNullOrWhiteSpace(entityLabelsArgument))
            {
                SendCommand(CreateFailure("Error", "The IFC isolation request did not include a source file and selected entity labels."));
                return;
            }

            var outputFolder = SelectOutputFolder();
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                SendCommand(new Message { Type = MessageType.ISOLATE_SINGLE_IFC_CANCELED });
                return;
            }

            try
            {
                var entityLabels = entityLabelsArgument
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse);

                Isolator.SplitByEntityLabels(sourceFilePath, outputFolder, entityLabels);
                SendCommand(new Message { Type = MessageType.ISOLATE_SINGLE_IFC_COMPLETED });
                return;
            }
            catch (Exception ex)
            {
                SendCommand(CreateFailure("Error", "There was an error while splitting the IFC.", ex));
            }
        }

        static string SelectOutputFolder()
        {
            using var folderDialog = new FolderBrowserDialog();
            folderDialog.Description = "Select the output folder for the split IFC file(s)";
            folderDialog.SelectedPath = string.IsNullOrWhiteSpace(_lastSelectedFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                : _lastSelectedFolder;

            var result = folderDialog.ShowDialog();
            if (result != DialogResult.OK || string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                return null;

            _lastSelectedFolder = folderDialog.SelectedPath;
            return folderDialog.SelectedPath;
        }

        static void SendCommand(Message command)
        {
            var serializedCommand = SerializeMessage(command);
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (true)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", "Core_To_Api_Pipe", PipeDirection.Out);

                    client.Connect(500);

                    using var writer = new StreamWriter(client);

                    writer.AutoFlush = true;
                    writer.WriteLine(serializedCommand);
                    return;
                }
                catch (TimeoutException) when (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(250);
                }
            }
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

        static Message CreateFailure(string title, string message)
        {
            return CreateFailure(title, message, null);
        }

        static Message CreateFailure(string title, string message, Exception exception)
        {
            return new Message
            {
                Type = MessageType.ISOLATE_SINGLE_IFC_FAILED,
                Arguments = new[]
                {
                    new Argument { Name = "Title", Value = title },
                    new Argument { Name = "Message", Value = message },
                    new Argument { Name = "Details", Value = exception?.ToString() },
                },
            };
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
    }
}
