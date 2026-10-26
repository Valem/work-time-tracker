; Work Time Tracker: per-user Inno Setup installer (ADR-002).
;
; Compile against a publish folder that holds wtt.exe and wtt-recorder.exe:
;   ISCC.exe /DPublishDir=<folder> [/DAppVersion=1.2.3] [/DOutputDir=<folder>] installer\wtt.iss
; tools\run.ps1 -Installer publishes both programs and compiles this script.
;
; Install:   wtt-setup.exe /VERYSILENT /CURRENTUSER            (no admin rights, %LocalAppData%\Programs)
; Fallback:  wtt-setup.exe /VERYSILENT /ALLUSERS               (admin, Program Files, Run value in HKLM)

#ifndef PublishDir
  #error PublishDir is not defined: ISCC.exe /DPublishDir=<folder with wtt.exe and wtt-recorder.exe> installer\wtt.iss
#endif
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef AppPublisher
  #define AppPublisher "Valem"
#endif

#define AppName "Work Time Tracker"
#define AppExe "wtt.exe"
#define RecorderExe "wtt-recorder.exe"
#define RunValueName "WorkTimeTracker"
#define DataFolderName "WorkTimeTracker"

#if !FileExists(AddBackslash(PublishDir) + AppExe)
  #error PublishDir does not contain wtt.exe
#endif
#if !FileExists(AddBackslash(PublishDir) + RecorderExe)
  #error PublishDir does not contain wtt-recorder.exe
#endif

[Setup]
AppId={{1D5BCBB4-EF22-42AE-B744-B03AA9CF1ACF}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Valem/work-time-tracker
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\WorkTimeTracker
DisableDirPage=yes
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir={#OutputDir}
OutputBaseFilename=wtt-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Pages are replaced whole on an upgrade, so no stale asset stays behind.
Type: filesandordirs; Name: "{app}\wwwroot"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; HKA is HKCU in a per-user install and HKLM in the per-machine fallback.
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#RunValueName}"; ValueData: """{app}\{#RecorderExe}"""; Flags: uninsdeletevalue

[Run]
; Starts the Recorder after every install and upgrade, as the signed-in user even when elevated.
Filename: "{app}\{#RecorderExe}"; Flags: nowait runasoriginaluser
Filename: "{app}\{#AppExe}"; Description: "Open {#AppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
; K3: the data folder goes with the program.
Type: filesandordirs; Name: "{localappdata}\{#DataFolderName}"

[Code]
function WerAddExcludedApplication(ExeName: String; AllUsers: BOOL): Integer;
  external 'WerAddExcludedApplication@wer.dll stdcall delayload';
function WerRemoveExcludedApplication(ExeName: String; AllUsers: BOOL): Integer;
  external 'WerRemoveExcludedApplication@wer.dll stdcall delayload';

function UserFilter(): String;
begin
  Result := ' /FI "USERNAME eq ' + GetUserNameString + '"';
end;

function ProgramRuns(const ExeName: String): Boolean;
var
  ResultCode: Integer;
  ListFile: String;
  Listing: AnsiString;
begin
  ListFile := ExpandConstant('{tmp}\running.txt');
  Result := Exec(ExpandConstant('{cmd}'),
      '/C ""' + ExpandConstant('{sys}\tasklist.exe') + '" /NH /FI "IMAGENAME eq ' + ExeName + '"' + UserFilter + ' > "' + ListFile + '""',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and LoadStringFromFile(ListFile, Listing)
    and (Pos(Lowercase(ExeName), Lowercase(String(Listing))) > 0);
end;

procedure StopProgram(const ExeName: String);
var
  ResultCode: Integer;
  Attempt: Integer;
begin
  { Asked to close first: both programs have a top-level window that ends them cleanly on WM_CLOSE (the
    Recorder's hidden signal window writes its last coverage and pending facts). Forced only when still
    running after 5 seconds. Only this user's processes are touched. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM ' + ExeName + UserFilter, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Attempt := 0;
  while ProgramRuns(ExeName) and (Attempt < 10) do
  begin
    Sleep(500);
    Attempt := Attempt + 1;
  end;
  if ProgramRuns(ExeName) then
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM ' + ExeName + UserFilter, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure ExcludeFromErrorReporting(const Exclude: Boolean);
begin
  { Crash reports stay on the laptop (ADR-006): Windows Error Reporting skips both programs. }
  try
    if Exclude then
    begin
      WerAddExcludedApplication('{#AppExe}', IsAdminInstallMode);
      WerAddExcludedApplication('{#RecorderExe}', IsAdminInstallMode);
    end
    else
    begin
      WerRemoveExcludedApplication('{#AppExe}', IsAdminInstallMode);
      WerRemoveExcludedApplication('{#RecorderExe}', IsAdminInstallMode);
    end;
  except
    Log('Windows Error Reporting exclusion not changed: ' + GetExceptionMessage);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopProgram('{#RecorderExe}');
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    ExcludeFromErrorReporting(True);
end;

function InitializeUninstall(): Boolean;
begin
  StopProgram('{#AppExe}');
  StopProgram('{#RecorderExe}');
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataFolder: String;
  Attempt: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    ExcludeFromErrorReporting(False);
    { The web view's helper processes may hold its files for a moment after the app has ended. }
    DataFolder := ExpandConstant('{localappdata}\{#DataFolderName}');
    Attempt := 0;
    while DirExists(DataFolder) and (Attempt < 10) do
    begin
      DelTree(DataFolder, True, True, True);
      if DirExists(DataFolder) then
        Sleep(500);
      Attempt := Attempt + 1;
    end;
  end;
end;
