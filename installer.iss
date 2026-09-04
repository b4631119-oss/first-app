#define MyAppName "My First App"

#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#define MyAppPublisher "justnpm"
#define MyAppExeName "MyFirstApp.exe"
#define MyAppId "{8D5F6C2A-7E91-4B9D-9C12-123456789ABC}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/b4631119-oss/first-app
AppSupportURL=https://github.com/b4631119-oss/first-app/issues
AppUpdatesURL=https://github.com/b4631119-oss/first-app/releases

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

OutputDir=..
OutputBaseFilename=MyFirstApp-Setup-{#MyAppVersion}

SetupIconFile=..\installer\app.ico

Compression=lzma/ultra64
SolidCompression=yes
WizardStyle=modern

PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

DisableDirPage=yes
DisableProgramGroupPage=yes

UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Types]
Type: "full"; Description: "Full installation"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Create a &Quick Launch icon"; GroupDescription: "Additional icons:"; Flags: unchecked; OnlyBelowVersion: 0,6.1

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Type: full

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[CloseApplications]
CloseApplications=force

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
end;