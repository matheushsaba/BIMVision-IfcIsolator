#define AppName "Find By Guid Plugin"
#define AppPublisher "Matheus Henrique Sabadin"
#define PluginFolder "FindByGuidPlugin"

#define EnvAppVersion GetEnv("FINDBYGUID_VERSION")
#if EnvAppVersion == ""
  #define AppVersion "1.2.0"
#else
  #define AppVersion EnvAppVersion
#endif

#define SourceRoot AddBackslash(SourcePath) + "payload"

[Setup]
AppId={{F16FB980-4057-4DD2-92A8-B17AE1F3C8CB}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={commonpf32}\Datacomp\BIM Vision
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=FindByGuidPluginSetup-{#AppVersion}
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{app}\plugins\{#PluginFolder}"
Name: "{app}\plugins_x64\{#PluginFolder}"

[Files]
Source: "{#SourceRoot}\plugins\{#PluginFolder}\*"; DestDir: "{app}\plugins\{#PluginFolder}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\plugins\FindByGuidPlugin.plg"; DestDir: "{app}\plugins"; Flags: ignoreversion
Source: "{#SourceRoot}\plugins_x64\{#PluginFolder}\*"; DestDir: "{app}\plugins_x64\{#PluginFolder}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\plugins_x64\FindByGuidPlugin.plg"; DestDir: "{app}\plugins_x64"; Flags: ignoreversion

[InstallDelete]
; Remove stale files from the old flat-layout plugin version.
Type: files; Name: "{app}\plugins\FindByGuidPlugin_x86.dll"
Type: files; Name: "{app}\plugins_x64\FindByGuidPlugin_x64.dll"

; Remove the previous folder-layout version before copying the new payload.
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}"
Type: filesandordirs; Name: "{app}\plugins_x64\{#PluginFolder}"

[UninstallDelete]
Type: files; Name: "{app}\plugins\FindByGuidPlugin.plg"
Type: files; Name: "{app}\plugins_x64\FindByGuidPlugin.plg"
Type: filesandordirs; Name: "{app}\plugins\{#PluginFolder}"
Type: filesandordirs; Name: "{app}\plugins_x64\{#PluginFolder}"

[Code]
function InitializeSetup(): Boolean;
begin
  { Future bundle hook:
    - Add more plugin components here or split shared logic into a common .iss include.
    - Keep each plugin in its own folder so one installer can upgrade/remove each payload independently. }
  Result := True;
end;
