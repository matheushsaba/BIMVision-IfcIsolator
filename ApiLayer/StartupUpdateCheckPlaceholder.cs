using System.Diagnostics;

namespace ApiLayer
{
    internal static class StartupUpdateCheckPlaceholder
    {
        public static void QueueGithubVersionCheck(string pluginDirectory)
        {
            // Future implementation hook:
            // 1. Read the installed plugin version from this assembly or installer metadata.
            // 2. Query the GitHub releases API for the latest approved installer.
            // 3. Notify the user or launch the installer after BIM Vision is closed.
            Trace.TraceInformation("Ifc Isolator update check placeholder for: " + pluginDirectory);
        }
    }
}
