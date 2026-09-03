; Nekoframe Inno Setup Script — Inno Setup 6+  https://jrsoftware.org/isinfo.php

#define AppName      "Nekoframe"
#define AppVersion   "1.0.0"
#define AppPublisher "Nubsuki"
#define AppExe       "Nekoframe.exe"
#define TaskName     "Nekoframe System Stats"
#define PublishDir   "..\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{F3A7B2C1-D845-4E9F-A312-88C7D6E01234}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename=NekoframeSetup
SetupIconFile=..\Assets\icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
AppMutex=NekoframeMutex
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "autostart"; Description: "Start Nekoframe automatically when Windows starts"; GroupDescription: "Windows Startup:"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""{#TaskName}"" /F"; RunOnceId: "RemoveStartupTask"; Flags: runhidden waituntilterminated

[Code]

// Registers the Windows Scheduled Task using XML so Nekoframe starts at logon with
// highest privileges — same approach as StartupRegistrar.cs inside the app.
// Done here so the task is ready before the user ever manually launches the app.
procedure CreateStartupTask(AppPath: string);
var
  XmlPath: string;
  XmlContent: string;
  ResultCode: Integer;
begin
  XmlPath := ExpandConstant('{tmp}\nekoframe_task.xml');

  XmlContent :=
    '<?xml version="1.0" encoding="UTF-16"?>' + #13#10 +
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' + #13#10 +
    '  <RegistrationInfo><Author>Nekoframe</Author></RegistrationInfo>' + #13#10 +
    '  <Triggers><LogonTrigger><Enabled>true</Enabled><Delay>PT5S</Delay></LogonTrigger></Triggers>' + #13#10 +
    '  <Principals><Principal id="Author"><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>' + #13#10 +
    '  <Settings>' + #13#10 +
    '    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' + #13#10 +
    '    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>' + #13#10 +
    '    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' + #13#10 +
    '    <AllowHardTerminate>false</AllowHardTerminate>' + #13#10 +
    '    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>' + #13#10 +
    '    <Enabled>true</Enabled>' + #13#10 +
    '  </Settings>' + #13#10 +
    '  <Actions Context="Author"><Exec><Command>' + AppPath + '</Command></Exec></Actions>' + #13#10 +
    '</Task>';

  SaveStringToFile(XmlPath, XmlContent, False);
  Exec(ExpandConstant('{sys}\schtasks.exe'),
    '/Create /TN "' + '{#TaskName}' + '" /XML "' + XmlPath + '" /F',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  DeleteFile(XmlPath);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('autostart') then
    CreateStartupTask(ExpandConstant('{app}\{#AppExe}'));
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption :=
      'Nekoframe has been installed.' + #13#10 + #13#10 +
      'Right-click the tray icon ' + #8594 + ' "Start with Windows" to toggle auto-start at any time.';
end;
