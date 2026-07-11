#define MyAppName "VibeDLNA"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "VibeDLNA"
#define MyAppExeName "VibeDLNA.exe"

[Setup]
AppId={{F5883734-79CA-4AA3-B8A8-F8D4A3B1714A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=VibeDLNA-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Assets\VibeDLNA.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
AppMutex=VibeDLNA.SingleInstance

[Files]
Source: "..\artifacts\publish\VibeDLNA.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\VibeDLNA"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\VibeDLNA"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir VibeDLNA"; Flags: nowait postinstall skipifsilent
