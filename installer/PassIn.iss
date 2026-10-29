; Per-user installer: no administrator rights, installs to %LOCALAPPDATA%\Programs\PassIn.
; Uninstalling never touches the vault in %APPDATA%\PassIn.
; Built by build.ps1 -Installer; pass /DAppVersion=x.y.z to stamp the version.

#define AppName "PassIn"
#define AppExe "PassIn.exe"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{B4E2C6A1-7F3D-4E58-9C21-6A8D0F4B3E71}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Said Muradkhan
AppPublisherURL=https://github.com/saidmuradkhan/PassIn
AppSupportURL=https://github.com/saidmuradkhan/PassIn/issues
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableDirPage=yes
; Versions up to 1.2 installed to Programs\KeyKhan; move upgrades to the new folder.
UsePreviousAppDir=no
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
WizardStyle=modern
SetupIconFile=..\src\PassIn.App\Resources\passin.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=PassIn-Setup
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: autostart; Description: "Start PassIn when I sign in to Windows"
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[InstallDelete]
; PassIn was called KeyKhan up to 1.2. The same AppId upgrades it, so remove what the old name left behind.
; The app itself moves the vault, settings and autostart entry on its first start.
Type: filesandordirs; Name: "{localappdata}\Programs\KeyKhan"
Type: files; Name: "{autoprograms}\KeyKhan.lnk"
Type: files; Name: "{autodesktop}\KeyKhan.lnk"

[Files]
Source: "..\artifacts\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; ValueType: string; \
  ValueData: """{app}\{#AppExe}"" --minimized"; Tasks: autostart
; The app can also enable autostart itself; always clean the value up on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; ValueType: none; \
  Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
; In-app updates run the installer silently with /RELAUNCH=1 so PassIn comes back afterwards.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: ShouldRelaunch

[Code]
function ShouldRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:relaunch|0}') = '1';
end;
