using BIMVision;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiLayer
{
    public class ApiLayer : Plugin
    {
        readonly string _assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        ApiWrapper _api;
        int _pluginButton;
        Process _coreProcess;
        bool _coreListenerStarted;

        public override void GetPluginInfo(ref PluginInfo info)
        {
            info.description = "Plugin to isolate elements into different IFC files.";
            info.name = "Ifc Isolator";
            info.email = "-";
            info.producer = "Matheus Henrique Sabadin";
            info.www = "https://www.linkedin.com/in/m-sab/";
            info.help_directory = _assemblyFolder;
        }

        public override byte[] GetPluginKey()
        {
            return new byte[] { };
        }

        public override void OnCallLimit()
        {
            MessageBox.Show("Call limit reached.");
        }

        public override void OnLoad(PLUGIN_ID pid, bool registered, IntPtr viewerHwnd)
        {
            _api = new ApiWrapper(pid);

            _pluginButton = _api.CreateButton(0, PluginButtonClick);
            _api.SetButtonImage(_pluginButton, _assemblyFolder + "logo32.png");
            _api.SetButtonSmallImage(_pluginButton, _assemblyFolder + "logo16.png");
            _api.SetGalleryItemImage(_pluginButton, _assemblyFolder + "logo32.png");
            _api.SetButtonText(_pluginButton, "Isolate Into Single IFC", "Isolate selected elements into a new unique IFC file.");
            _api.EnableButton(_pluginButton, true);

            StartCoreListener();
        }

        public override void OnUnload()
        {
            if (_coreProcess != null && !_coreProcess.HasExited)
            {
                _coreProcess.Kill();
                _coreProcess.Dispose();
            }
        }

        void PluginButtonClick()
        {
            var selectedObjects = _api.GetSelected();
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                _api.MessageBox("Error", "Objects need to be selected to split an IFC.", 0);
                return;
            }

            var sourceFilePath = _api.GetLoadedIfcPath();
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                _api.MessageBox("Error", "No loaded IFC file path was found.", 0);
                return;
            }

            var entityLabels = selectedObjects
                .Select(x => _api.GetObjectInfo(x).ifc_entity_number)
                .ToArray();

            var entityLabelsArgument = string.Join(" ", entityLabels);

            var exePath = Path.Combine(_assemblyFolder, "CoreLayer.exe");

            if (!EnsureCoreProcessStarted(exePath))
                return;

            try
            {
                SendCommand(new Message
                {
                    Type = MessageType.ISOLATE_SINGLE_IFC_REQUEST,
                    Arguments = new[]
                    {
                        new Argument { Name = "SourceFilePath", Value = sourceFilePath },
                        new Argument { Name = "EntityLabels", Value = entityLabelsArgument },
                    },
                });
            }
            catch (TimeoutException)
            {
                _api.MessageBox("Error", "CoreLayer did not respond in time. Please try again.", 0);
            }
            catch (Exception ex)
            {
                _api.MessageBox("Error", "Unable to start IFC isolation: " + ex.Message, 0);
            }
        }

        bool EnsureCoreProcessStarted(string exePath)
        {
            if (_coreProcess != null && !_coreProcess.HasExited)
                return true;

            if (!File.Exists(exePath))
            {
                _api.MessageBox("Error", "CoreLayer.exe not found:\n" + exePath, 0);
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true,
            };

            _coreProcess = Process.Start(psi);
            return _coreProcess != null;
        }

        void StartCoreListener()
        {
            if (_coreListenerStarted)
                return;

            _coreListenerStarted = true;

            Task.Run(() =>
            {
                while (true)
                {
                    using (var server = new NamedPipeServerStream("Core_To_Api_Pipe", PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.None))
                    {
                        server.WaitForConnection();

                        using (var reader = new StreamReader(server))
                        {
                            var msgString = reader.ReadLine();

                            if (msgString != null)
                            {
                                var message = DeserializeMessage(msgString);

                                switch (message.Type)
                                {
                                    case MessageType.ISOLATE_SINGLE_IFC_COMPLETED:
                                        _api.MessageBox("Success", "The IFC was exported correctly.", 0);
                                        break;
                                    case MessageType.ISOLATE_SINGLE_IFC_FAILED:
                                        _api.MessageBox(GetArgument(message, "Title", "Error"), GetArgument(message, "Message", "There was an error while splitting the IFC."), 0);
                                        break;
                                    case MessageType.ISOLATE_SINGLE_IFC_CANCELED:
                                        break;
                                    default: break;
                                }
                            }
                        }
                    }
                }
            });
        }

        static void SendCommand(Message command)
        {
            var serializedCommand = SerializeMessage(command);
            var deadline = DateTime.UtcNow.AddSeconds(10);

            while (true)
            {
                try
                {
                    using (var client = new NamedPipeClientStream(".", "Api_To_Core_Pipe", PipeDirection.Out))
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
                    Thread.Sleep(250);
                }
            }
        }

        static string GetArgument(Message message, string name, string defaultValue)
        {
            if (message.Arguments == null)
                return defaultValue;

            var argument = message.Arguments.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(argument?.Value) ? defaultValue : argument.Value;
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
