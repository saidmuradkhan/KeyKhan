; Per-user installer: no administrator rights, installs to %LOCALAPPDATA%\Programs\KeyKhan.
; Built by build.ps1 -Installer; pass /DAppVersion=x.y.z to stamp the version.

#define AppName "KeyKhan"
#define AppExe "KeyKhan.exe"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{B4E2C6A1-7F3D-4E58-9C21-6A8D0F4B3E71}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Said Muradkhan
AppPublisherURL=https://github.com/saidmuradkhan/KeyKhan
AppSupportURL=https://github.com/saidmuradkhan/KeyKhan/issues
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
WizardStyle=modern
SetupIconFile=..\src\KeyKhan.App\Resources\keykhan.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=KeyKhan-Setup
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: autostart; Description: "Start KeyKhan when I sign in to Windows"
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

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

; The vault (%APPDATA%\KeyKhan) is deliberately left in place on uninstall.
