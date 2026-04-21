using BIMVision;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
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
            if (_coreProcess != null && !_coreProcess.HasExited)
                return;

            Task.Run(() => InitializeCoreListener());

            var exePath = Path.Combine(_assemblyFolder, "CoreLayer.exe");

            if (!File.Exists(exePath))
            {
                MessageBox.Show("CoreLayer.exe not found:\n" + exePath);

                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true,
            };

            _coreProcess = Process.Start(psi);
        }
        void InitializeCoreListener()
        {
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
                                var message = JsonConvert.DeserializeObject<Message>(msgString);

                                switch (message.Type)
                                {
                                    case MessageType.CORE_STATUS_READY:
                                        {
                                            MessageBox.Show("ApiLayer: CORE_STATUS_READY command received.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        }
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
            using (var client = new NamedPipeClientStream(".", "Api_To_Core_Pipe", PipeDirection.Out))
            {
                client.Connect(2000);

                using (var writer = new StreamWriter(client))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(JsonConvert.SerializeObject(command));
                }
            }
        }
    }
}