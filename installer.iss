#define MyAppName "My First App"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "justnpm"
#define MyAppExeName "MyFirstApp.exe"

[Setup]
AppId={{8D5F6C2A-7E91-4B9D-9C12-123456789ABC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\My First App
DefaultGroupName=My First App

OutputDir=installer
OutputBaseFilename=MyFirstApp-Setup-{#MyAppVersion}

Compression=lzma
SolidCompression=yes

WizardStyle=modern

PrivilegesRequired=admin

[Files]
Source: "bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\My First App"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\My First App"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить My First App"; Flags: nowait postinstall skipifsilent
