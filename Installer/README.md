# Ifc Isolator Plugin Installer

The installer is built with Inno Setup, which is free to use and produces a normal Windows `.exe` installer.

## Build

1. Install Visual Studio Build Tools with MSBuild support.
2. Install Inno Setup 6.
3. From the repository root, run:

```powershell
.\Installer\build-installer.ps1
```

The script reads the plugin version from `Directory.Build.props`, builds both `Release|x86` and `Release|x64`, stages the payload, and writes the installer to `Installer\output`.

To only stage the payload without compiling the installer:

```powershell
.\Installer\build-installer.ps1 -StageOnly
```

## Installed Layout

The installer uses the installing machine's 32-bit Program Files folder, not a hardcoded user path:

```text
{commonpf32}\Datacomp\BIM Vision\plugins\IfcIsolatorPlugin
{commonpf32}\Datacomp\BIM Vision\plugins_x64\IfcIsolatorPlugin
```

It also writes the BIM Vision marker files:

```text
{commonpf32}\Datacomp\BIM Vision\plugins\IfcIsolatorPlugin.plg
{commonpf32}\Datacomp\BIM Vision\plugins_x64\IfcIsolatorPlugin.plg
```

## Upgrade Behavior

Each install removes the previous `IfcIsolatorPlugin` folders before copying the new version. It also removes the old flat-layout files:

```text
plugins\IfcIsolatorPlugin_x86.dll
plugins\IfcIsolatorTerminal_x64.exe
plugins_x64\IfcIsolatorPlugin_x64.dll
plugins_x64\IfcIsolatorPlugin_x64.dlll
plugins_x64\IfcIsolatorTerminal_x64.exe
```

## Future Bundle

The Inno script keeps this plugin isolated in its own folder. A future bundle installer can add another plugin as a separate component or include, while sharing the same BIM Vision base folder and upgrade rules.
