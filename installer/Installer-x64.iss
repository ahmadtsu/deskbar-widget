#define MyAppName "DeskbarWidget"
#define MyAppPublisher "DeskbarWidget"
#define MyAppURL "https://github.com/ahmadtsu/deskbar-widget"
#define MyAppExeName "DeskbarWidget.exe"
#define PublishDir "..\publish"

#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif

#define ArchitecturesAllowed "x64os"
#define ArchitecturesInstallIn64BitMode "x64os"
#define OutputBaseName "DeskbarWidget-Setup-x64"

#include "InnoDependencyInstaller\CodeDependencies.iss"
#include "DotNetInstaller.iss"

[Setup]
AppId={{8F5B8997-5B2E-4D2A-98C3-7B51203E3F4A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed={#ArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#ArchitecturesInstallIn64BitMode}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=output
OutputBaseFilename={#OutputBaseName}
SetupIconFile=..\assets\eyes.ico
SolidCompression=yes
Compression=lzma2/ultra64
WizardStyle=modern
DisableWelcomePage=no
CloseApplications=force

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion restartreplace uninsrestartdelete
Source: "..\assets\eyes.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\eyes.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\eyes.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillAppOnUninstall"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure KillAppIfRunning;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM "{#MyAppExeName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM "DeskbarWidget.App.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup: Boolean;
begin
  KillAppIfRunning;
  AddDotNet90DesktopDependency;
  Result := True;
end;

function InitializeUninstall: Boolean;
begin
  KillAppIfRunning;
  Result := True;
end;
