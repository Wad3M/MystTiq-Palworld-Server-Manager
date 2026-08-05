#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#define MyAppName "MystTiq Palworld Server Manager"
#define MyAppExeName "MystTiqPalworldServer.exe"
#define SourceDir "..\artifacts\publish\win-x64"

[Setup]
AppId={{5E01900A-7A21-4CD8-8ABF-76E413BDA7D7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=MystTiq Contributors
DefaultDirName={autopf}\MystTiq Palworld Server Manager
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=MystTiqPalworldServer-v{#MyAppVersion}-win-x64-setup
SetupIconFile=..\src\PalworldManager\Assets\PalworldServerManager.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
LicenseFile=..\LICENSE

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
