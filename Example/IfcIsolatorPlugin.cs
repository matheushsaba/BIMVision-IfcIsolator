using System;
using System.Linq;


namespace Example
{
    using System.Diagnostics;
    using System.Windows.Forms;

    using BIMVision;

    class IfcIsolatorPlugin : Plugin
    {
        const string SPLITTER_EXECUTABLE_NAME = "IfcIsolatorTerminal_x64.exe";

        private ApiWrapper api;

        private OBJECT_ID allId;
        private int _isolateMultipleIfcs;
        private int _isolateSingleIfc;
        private static string _lastSelectedFolder = string.Empty;

        public override void GetPluginInfo(ref PluginInfo info)
        {
            info.name = "Ifc Isolator";
            info.producer = "Matheus Henrique Sabadin";
            info.www = "www.linkedin.com/in/m-sab/";
            info.email = "-";
            info.description = "Plugin to isolate elements into different IFC files.";
            //info.help_directory = "";
        }

        public override byte[] GetPluginKey()
        {
            //byte[] plugin_key ={......};
            //return plugin_key;
            return null;
        }

        public override void OnLoad(PLUGIN_ID pid, bool registered, IntPtr viewerHwnd)
        {
            api = new ApiWrapper(pid);

            api.OnModelLoad(onModelLoad);

            _isolateSingleIfc = api.CreateButton(0, IsolateSingleIfc);
            api.SetButtonText(_isolateSingleIfc, "Isolate Into Single IFC", "Isolate selected elements into a new unique IFC file.");

            //_isolateMultipleIfcs = api.CreateButton(0, IsolateMultipleIfcs);
            //api.SetButtonText(_isolateMultipleIfcs, "Isolate Into Multiple IFCs", "Isolate each selected elements into a new IFC file.");
        }

        public override void OnCallLimit()
        {
        }

        public override void OnUnload()
        {
        }

        public override void OnGuiColorsChange()
        {
        }

        private void onModelLoad()
        {
            allId = api.GetAllObjectsId();
        }

        private void IsolateMultipleIfcs()
        {
            //api.Select(allId, true);
            //try
            //{
            //    api.Invalidate();
            //}
            //catch (BIMVision.DemoModeCallLimitException)
            //{
            //}
        }

        private void IsolateSingleIfc()
        {
            var selectedObjects = api.GetSelected();
            if (selectedObjects is null)
            {
                api.MessageBox("Error", "Objects need to be selected to split an IFC.", 0);
                return;
            }

            // Prompt the user to pick an output folder
            string outputFolder = string.Empty;
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the output folder for the split IFC file(s)";

                // Use last selected path, or Desktop if none
                folderDialog.SelectedPath = string.IsNullOrWhiteSpace(_lastSelectedFolder)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : _lastSelectedFolder;

                var result = folderDialog.ShowDialog();

                var isResultValid = result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath);
                if (isResultValid)
                {
                    outputFolder = folderDialog.SelectedPath;
                    _lastSelectedFolder = outputFolder;
                }
                else
                {
                    // The user canceled the dialog or did not select a folder
                    return;
                }
            }

            var sourceFilePath = api.GetLoadedIfcPath();

            var entityLabels = selectedObjects
                .Select(x => api.GetObjectInfo(x).ifc_entity_number)
                .ToArray();

            var entityLabelsArgument = $"\"{string.Join(" ", entityLabels)}\"";

            var isolatorProcess = RunProcess(outputFolder, sourceFilePath, entityLabelsArgument);
            if (isolatorProcess.ExitCode == 0)
            {
                api.MessageBox("Success", "The IFC was exported correctly.", 0);
            }
            else
            {
                api.MessageBox("Error", "There was an error while splitting the IFC.", 0);
            }
        }

        private static Process RunProcess(string outputFolder, string sourceFilePath, string entityLabelsArgument)
        {
            var splitterProcess = new Process();
            splitterProcess.StartInfo.UseShellExecute = false;
            splitterProcess.StartInfo.CreateNoWindow = true;
            splitterProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            splitterProcess.StartInfo.FileName = SPLITTER_EXECUTABLE_NAME;
            splitterProcess.StartInfo.Arguments = $"{sourceFilePath} {outputFolder} {entityLabelsArgument}";
            splitterProcess.StartInfo.RedirectStandardError = false;
            splitterProcess.StartInfo.RedirectStandardOutput = false;
            splitterProcess.StartInfo.UseShellExecute = false;
            splitterProcess.StartInfo.CreateNoWindow = true;
            splitterProcess.Start();
            splitterProcess.WaitForExit();

            return splitterProcess;
        }
    }
}
