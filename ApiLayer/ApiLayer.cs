using BIMVision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        const string GuidPropertySetName = " Element Specific";
        const string GuidPropertyName = "Guid";

        readonly string _assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly ManualResetEventSlim _coreReadyEvent = new ManualResetEventSlim(false);
        ApiWrapper _api;
        int _pluginButton;
        Process _coreProcess;
        bool _coreListenerStarted;
        IntPtr _viewerHwnd;
        Control _viewer;

        public override void GetPluginInfo(ref PluginInfo info)
        {
            info.description = "Plugin to find elements by its Guid.";
            info.name = "Find By Guid";
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
            ShowError("Error", "Call limit reached.");
        }

        public override void OnLoad(PLUGIN_ID pid, bool registered, IntPtr viewerHwnd)
        {
            _api = new ApiWrapper(pid);
            _viewerHwnd = viewerHwnd;
            _viewer = Control.FromHandle(viewerHwnd);
            StartupUpdateCheckPlaceholder.QueueGithubVersionCheck(_assemblyFolder);

            _pluginButton = _api.CreateButton(0, PluginButtonClick);
            _api.SetButtonImage(_pluginButton, Path.Combine(_assemblyFolder, "logo32.png"));
            _api.SetButtonSmallImage(_pluginButton, Path.Combine(_assemblyFolder, "logo16.png"));
            _api.SetGalleryItemImage(_pluginButton, Path.Combine(_assemblyFolder, "logo32.png"));
            _api.SetButtonText(_pluginButton, "Find By GUID", "Searches and selects the object with the given Global ID.");
            _api.EnableButton(_pluginButton, true);

            StartCoreListener();
        }

        public override void OnUnload()
        {
            _coreReadyEvent.Reset();

            if (_coreProcess != null)
            {
                if (!_coreProcess.HasExited)
                {
                    _coreProcess.Kill();
                }

                _coreProcess.Dispose();
                _coreProcess = null;
            }
        }

        void PluginButtonClick()
        {
            var exePath = Path.Combine(_assemblyFolder, "CoreLayer.exe");

            if (!EnsureCoreProcessStarted(exePath))
                return;

            if (!_coreReadyEvent.IsSet)
            {
                _coreReadyEvent.Wait(TimeSpan.FromSeconds(2));
            }

            try
            {
                SendCommand(new Message
                {
                    Type = MessageType.SHOW_FIND_BY_GUID_WINDOW,
                    Arguments = new[]
                    {
                        new Argument
                        {
                            Name = "ViewerHandle",
                            Value = _viewerHwnd.ToInt64().ToString(CultureInfo.InvariantCulture),
                        },
                    },
                });
            }
            catch (TimeoutException ex)
            {
                ShowError("Error", "CoreLayer did not respond in time. Please try again.", ex);
            }
            catch (Exception ex)
            {
                ShowError("Error", "Unable to open the GUID search window.", ex);
            }
        }

        bool EnsureCoreProcessStarted(string exePath)
        {
            if (_coreProcess != null)
            {
                if (!_coreProcess.HasExited)
                    return true;

                _coreProcess.Dispose();
                _coreProcess = null;
            }

            if (!File.Exists(exePath))
            {
                ShowError("Error", "CoreLayer.exe not found:\n" + exePath);
                return false;
            }

            _coreReadyEvent.Reset();

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

                            if (msgString == null)
                                continue;

                            var message = DeserializeMessage(msgString);

                            switch (message.Type)
                            {
                                case MessageType.APP_READY:
                                    _coreReadyEvent.Set();
                                    break;
                                case MessageType.FIND_BY_GUID_REQUEST:
                                    DispatchToViewer(() => HandleFindByGuidRequest(message));
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            });
        }

        void DispatchToViewer(Action action)
        {
            if (_viewer != null && !_viewer.IsDisposed && _viewer.InvokeRequired)
            {
                _viewer.BeginInvoke(action);
                return;
            }

            action();
        }

        void HandleFindByGuidRequest(Message message)
        {
            var guid = GetArgument(message, "Guid", string.Empty);
            if (string.IsNullOrWhiteSpace(guid))
            {
                SendFindByGuidCompleted(0);
                return;
            }

            try
            {
                var objectsWithGuid = GetObjectsWithGuid(guid.Trim());

                if (objectsWithGuid.Count == 1)
                {
                    SetObjectStatus(objectsWithGuid[0]);
                }
                else if (objectsWithGuid.Count > 1)
                {
                    SetObjectsStatus(objectsWithGuid);
                }

                SendFindByGuidCompleted(objectsWithGuid.Count);
            }
            catch (Exception ex)
            {
                ShowError("Error", "Unable to search for the requested GUID.", ex);
            }
        }

        List<OBJECT_ID> GetObjectsWithGuid(string guid)
        {
            var objectsWithGuid = new List<OBJECT_ID>();
            var allObjectIds = _api.GetAllObjects();

            if (allObjectIds == null)
                return objectsWithGuid;

            foreach (var objectId in allObjectIds)
            {
                string objectGuid;

                if (TryGetObjectGuid(objectId, out objectGuid) && string.Equals(objectGuid, guid, StringComparison.Ordinal))
                {
                    objectsWithGuid.Add(objectId);
                }
            }

            return objectsWithGuid;
        }

        bool TryGetObjectGuid(OBJECT_ID objectId, out string guid)
        {
            guid = string.Empty;

            var guidProperties = _api.FilterProperties(objectId, GuidPropertySetName, GuidPropertyName);
            if (guidProperties != null)
            {
                foreach (var guidProperty in guidProperties)
                {
                    var value = guidProperty.value.value_str;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        guid = value;
                        return true;
                    }
                }
            }

            var propertySets = _api.GetObjectProperties(objectId, 0);
            if (propertySets == null)
                return false;

            var property = propertySets.FirstOrDefault(x => string.Equals(x.name, GuidPropertyName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(property.value_str))
                return false;

            guid = property.value_str;
            return true;
        }

        void SetObjectStatus(OBJECT_ID objectId)
        {
            _api.Select(objectId, true);
            _api.ZoomToObjects(new[] { objectId }, 1);
            _api.SetVisibleObject(objectId, VisibleType.vis_visible);
            _api.SetObjectVisible(objectId, 1, false);
            _api.SetObjectActive(objectId, true, false);
        }

        void SetObjectsStatus(List<OBJECT_ID> objectIds)
        {
            var objectIdsArray = objectIds.ToArray();

            _api.SelectMany(objectIdsArray, SelectType.select_with_openings);
            _api.ZoomToObjects(objectIdsArray, 1);
            _api.SetVisibleManyObjects(objectIdsArray, VisibleType.vis_visible);
            _api.SetVisibleMany(objectIdsArray, VisibleType.vis_visible);

            foreach (var objectId in objectIdsArray)
            {
                _api.SetObjectActive(objectId, true, false);
            }
        }

        void SendFindByGuidCompleted(int matchCount)
        {
            try
            {
                SendCommand(new Message
                {
                    Type = MessageType.FIND_BY_GUID_COMPLETED,
                    Arguments = new[]
                    {
                        new Argument
                        {
                            Name = "MatchCount",
                            Value = matchCount.ToString(CultureInfo.InvariantCulture),
                        },
                    },
                });
            }
            catch (Exception)
            {
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

        void ShowError(string title, string message)
        {
            CopyableErrorDialog.Show(title, message, null, _viewerHwnd);
        }

        void ShowError(string title, string message, Exception exception)
        {
            CopyableErrorDialog.Show(title, message, exception?.ToString(), _viewerHwnd);
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
