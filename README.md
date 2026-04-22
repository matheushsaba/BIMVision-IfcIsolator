# BIMVision-IfcIsolator

## Tested BIMvision version: 3.1.2

## What Is Ifc Isolator?

**Ifc Isolator** is a BIMvision plugin that exports the currently selected IFC elements into a new IFC file.

The plugin adds an **Isolate Into Single IFC** button to BIMvision. When the button is clicked, it reads the selected BIMvision objects, resolves their IFC entity labels, asks where the exported file should be written, and calls the [IfcIsolator](https://github.com/matheushsaba/IfcIsolator) engine to create the isolated IFC output.

### What It Does

- Uses the elements selected in the open BIMvision model.
- Reads the source IFC file path from BIMvision.
- Prompts the user for an output folder.
- Creates a new IFC file containing the selected entities.
- Shows a success message when the export completes.
- Shows a copyable error dialog when the export fails.

This is useful when you need to break down a large IFC model or share only a relevant part of a project.

![preview_plugin_compressed](https://github.com/user-attachments/assets/ea19485d-b2f6-4321-bb0c-32a12184de1f)

---

## Architecture

This project follows the newer BIMvision .NET plugin architecture described in the BIMvision SDK examples: a small **ApiLayer** is loaded by BIMvision, while the real application work runs in a separate **CoreLayer** process.

### ApiLayer

`ApiLayer` is the DLL that BIMvision loads as the plugin.

- Targets **.NET Framework 4.5.2**, because this is the runtime BIMvision can load directly.
- Uses the BIMvision SDK wrapper in `ApiLayer/BIMVision.cs`.
- Uses the `UnmanagedExports` NuGet package to expose the unmanaged plugin entry points expected by BIMvision.
- Registers the plugin button, icon, text, and plugin metadata.
- Reads the current BIMvision selection and loaded IFC file path.
- Starts `CoreLayer.exe` when needed.
- Sends isolation requests to CoreLayer through the `Api_To_Core_Pipe` named pipe.
- Listens for completion, cancellation, and failure messages on `Core_To_Api_Pipe`.
- Kills the CoreLayer process when BIMvision unloads the plugin.

### CoreLayer

`CoreLayer` is a separate Windows executable that contains the actual isolation logic.

- Currently targets **`net10.0-windows7.0`** with Windows Forms enabled.
- Listens for commands from ApiLayer through named pipes.
- Opens a folder picker for the output directory.
- Calls `IfcIsolator.Isolator.SplitByEntityLabels(...)`.
- Sends success, cancellation, or detailed failure messages back to ApiLayer.
- Copies the required XBim and IfcIsolator dependencies from the `Dependencies` folder into the plugin output.

This separation lets BIMvision load a compatible .NET Framework plugin DLL while the heavier IFC work can run in a modern .NET executable.

---

## Installation

1. Download the installer from the [Releases](https://github.com/matheushsaba/BIMVision-IfcIsolator/releases) section.
2. Run the installer as administrator.
3. Restart BIMvision if it was already open.
4. Open an IFC model, select one or more elements, and click **Isolate Into Single IFC**.

The installer writes the x86 and x64 plugin layouts automatically:

```text
C:\Program Files (x86)\Datacomp\BIM Vision\plugins\IfcIsolatorPlugin
C:\Program Files (x86)\Datacomp\BIM Vision\plugins_x64\IfcIsolatorPlugin
```

It also creates the BIMvision marker files:

```text
C:\Program Files (x86)\Datacomp\BIM Vision\plugins\IfcIsolatorPlugin.plg
C:\Program Files (x86)\Datacomp\BIM Vision\plugins_x64\IfcIsolatorPlugin.plg
```
---

## Building the Project

### Development Requirements

- [BIMvision](https://bimvision.eu/) 3.1.2 for the tested runtime environment.
- [.NET Framework 4.5.2 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net452) for the BIMvision-loaded ApiLayer.
- A Visual Studio or Build Tools installation with MSBuild support. The installer script looks for the latest MSBuild through `vswhere`, so **Microsoft Build Tools 2015 is not hardcoded as a requirement**.
- A .NET SDK capable of building `net10.0-windows7.0`, which is the current CoreLayer target.
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) if you want to compile the Windows installer.

The only NuGet package currently referenced by `ApiLayer` is:

```text
UnmanagedExports 1.2.7
```

It is already listed in `ApiLayer/packages.config` and imported by `ApiLayer/ApiLayer.csproj`. `UnmanagedExports.Repack` is not referenced by the current project.

![Build Screenshot](https://github.com/user-attachments/assets/13869e4b-a665-49da-b394-3e310299b653)

### Build the Installer

From the repository root:

```powershell
.\Installer\build-installer.ps1
```

The script:

- Reads the plugin version from `Directory.Build.props`.
- Builds both `Release|x86` and `Release|x64`.
- Stages the plugin payload under `Installer\payload`.
- Verifies the expected DLL, EXE, and `.plg` files exist.
- Compiles the Inno Setup installer into `Installer\output`.

To stage the payload without compiling the installer:

```powershell
.\Installer\build-installer.ps1 -StageOnly
```

For more installer details, see `Installer/README.md`.

---

## Development Notes

The Visual Studio project output paths point directly to the BIMvision plugin folders for debugging:

```text
C:\Program Files (x86)\Datacomp\BIM Vision\plugins\IfcIsolatorPlugin
C:\Program Files (x86)\Datacomp\BIM Vision\plugins_x64\IfcIsolatorPlugin
```

Because those folders are under Program Files, direct Debug or Release builds from Visual Studio may need elevated permissions. The installer build script avoids that by overriding the output path to `Installer\payload`.

Use the `x86` configuration for 32-bit BIMvision and the `x64` configuration for 64-bit BIMvision. The solution intentionally defines both platforms.

---

## Troubleshooting

### Plugin Not Appearing in the BIMvision Ribbon

- Confirm BIMvision 3.1.2 is installed. This is the version the current plugin was tested with.
- Make sure the installer completed successfully and BIMvision was restarted afterward.
- Check that the matching architecture folder exists: `plugins` for x86, `plugins_x64` for x64.
- Check that the `.plg` marker file exists next to the plugin folder.

![image](https://github.com/user-attachments/assets/ca2c58e3-f1fc-4eb5-8c22-e8050c7d3a27)

### Build Fails Around .NET Framework 4.5.2

Install the .NET Framework 4.5.2 Developer Pack. The ApiLayer must target .NET Framework 4.5.2 because BIMvision loads that DLL directly.

### Build Fails Around UnmanagedExports

The current project only references `UnmanagedExports` 1.2.7. Restore NuGet packages and rebuild.

If the export task cannot find required SDK tools such as `ildasm`, `ilasm`, or `lib.exe`, install the relevant Visual Studio Build Tools components. On older machines, Microsoft Build Tools 2015 may still satisfy that legacy toolchain, but the current installer script is written to use the latest available MSBuild.

### CoreLayer Does Not Start

The ApiLayer expects `CoreLayer.exe` to sit in the same plugin folder as `IfcIsolatorPlugin.dll`. Reinstall the plugin or rebuild both platforms with the installer script so the folder contains the DLL, EXE, dependencies, icons, and marker file together.

The current CoreLayer build is framework-dependent. If `CoreLayer.exe` is present but does not launch, make sure the target machine has a compatible .NET Desktop Runtime for `net10.0-windows7.0`, or publish CoreLayer as self-contained for release builds.
