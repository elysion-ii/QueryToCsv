; QueryToCsv Installer Script
; MyAppVersion is injected by Installer.ps1 (/D) from Directory.Build.props <Version>.
; Never hardcode a version here — Directory.Build.props is the single source of truth.
#ifndef MyAppVersion
  #error MyAppVersion is not defined. Build via Installer.ps1, which injects it from Directory.Build.props.
#endif
#define MyAppName "QueryToCsv"
#define MyAppExeName "QueryToCsv.exe"

[Setup]
AppId={{2756B9BF-C9B9-4C77-915D-1D10F9C31F50}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={userpf}\{#MyAppName}
DisableProgramGroupPage=yes
ChangesEnvironment=yes
OutputDir=Installer
OutputBaseFilename=QueryToCsv-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

; Update over a running installation. CloseApplications is Inno Setup's own
; Restart Manager support: Setup detects the processes holding files this install
; replaces, asks whether to close them, and restarts them afterwards. It is the
; default value, and is stated here so the intent survives an edit of this file.
;
; AppMutex is deliberately NOT declared. AppMutex blocks at startup, before the
; Restart Manager page is reached, which would downgrade an update from "closed
; and restarted automatically" to "close it yourself and start over". It also
; cannot be limited to uninstall, which is the path that actually needs it.
CloseApplications=yes

[Tasks]
Name: "addtopath"; Description: "Add to PATH environment variable"; GroupDescription: "Additional options:"

[Files]
; Ship the whole publish output, not just the executable: a native dependency
; adds files beside it (see docs/rules/dotnet.md, NATIVEDEP). The runtime folders are
; excluded because running the app from the build output fills them with local data;
; [Dirs] below creates them empty at the install target.
Source: "QueryToCsv\*"; Excludes: "appsettings.json,logs\*,queries\*,output\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; appsettings is user config, only copy on first install to preserve user settings
Source: "QueryToCsv\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Dirs]
; create queries and output folders
Name: "{app}\queries"
Name: "{app}\output"

[CustomMessages]
AppRunningCaption={#MyAppName} is still running
AppRunningText=The uninstaller cannot remove files that {#MyAppName} still has open.%n%nChoose how to continue.
AppRunningCloseFailedCaption={#MyAppName} did not close
AppRunningCloseFailedText={#MyAppName} was asked to close but is still running. It may be waiting for an answer, such as whether to save your work.%n%nClose it yourself and choose Retry, or cancel the uninstall.
AppRunningCloseAndRetry=Close and retry%nShut {#MyAppName} down, then continue the uninstall
AppRunningRetry=Retry%nI have closed it myself; check again
AppRunningCancel=Cancel%nStop the uninstall and leave {#MyAppName} installed

; Uninstall over a running application.
;
; CloseApplications above covers Setup only - Inno Setup's close-applications
; support does not extend to the uninstaller, whose command line carries no
; /NOCLOSEAPPLICATIONS either. The same Restart Manager is therefore driven from
; script below, so uninstalling a running application offers to close it instead
; of failing to delete files that are still locked.
[Code]
const
  RM_SUCCESS = 0;
  RM_MORE_DATA = 234;
  // lActionFlags for RmShutdown. RmForceShutdown (1) is deliberately never used:
  // a forced shutdown discards whatever the user has not saved. When a graceful
  // shutdown does not take, the choice goes back to the user.
  RM_SHUTDOWN_GRACEFUL = 0;
  // RmStartSession writes a session key of CCH_RM_SESSION_KEY+1 wide characters
  // into a buffer the caller supplies.
  RM_SESSION_KEY_BUFFER = '0000000000000000000000000000000000000000000000000000000000000000';

// Setup.e32 is a 32-bit executable, so a pointer is four bytes and fits a Cardinal.
// rgsFilenames is declared "var Cardinal" on purpose: Pascal Script then passes the
// address of that variable, which is exactly the one-element LPWSTR array the
// function expects. Registrations accumulate across calls, so several files are
// registered by calling RmRegisterResources once per file - Pascal Script cannot
// build a multi-element native pointer array, and does not need to.
function RmStartSession(var pSessionHandle: Cardinal; dwSessionFlags: Cardinal;
  strSessionKey: Cardinal): Cardinal;
  external 'RmStartSession@rstrtmgr.dll stdcall uninstallonly';

function RmRegisterResources(dwSessionHandle: Cardinal; nFiles: Cardinal;
  var rgsFilenames: Cardinal; nApplications: Cardinal; rgApplications: Cardinal;
  nServices: Cardinal; rgsServiceNames: Cardinal): Cardinal;
  external 'RmRegisterResources@rstrtmgr.dll stdcall uninstallonly';

function RmGetList(dwSessionHandle: Cardinal; var pnProcInfoNeeded: Cardinal;
  var pnProcInfo: Cardinal; rgAffectedApps: Cardinal;
  var lpdwRebootReasons: Cardinal): Cardinal;
  external 'RmGetList@rstrtmgr.dll stdcall uninstallonly';

function RmShutdown(dwSessionHandle: Cardinal; lActionFlags: Cardinal;
  fnStatus: Cardinal): Cardinal;
  external 'RmShutdown@rstrtmgr.dll stdcall uninstallonly';

function RmEndSession(dwSessionHandle: Cardinal): Cardinal;
  external 'RmEndSession@rstrtmgr.dll stdcall uninstallonly';

var
  // The registered addresses point into this array, so it outlives every session.
  AppExecutables: TArrayOfString;

// Collects every *.exe under the installation directory except the uninstaller
// itself, which is the process running this code and must not be shut down.
// Only executables are collected, never libraries: registering a DLL would make
// the Restart Manager report whichever host process loaded it, so a shell
// extension would drag Explorer into the list.
procedure CollectAppExecutables(const Directory, UninstallerPath: String);
var
  FindRec: TFindRec;
  FullPath: String;
begin
  if not FindFirst(AddBackslash(Directory) + '*', FindRec) then
    Exit;
  try
    repeat
      if (FindRec.Name = '.') or (FindRec.Name = '..') then
        Continue;
      FullPath := AddBackslash(Directory) + FindRec.Name;
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        CollectAppExecutables(FullPath, UninstallerPath)
      else if (CompareText(ExtractFileExt(FindRec.Name), '.exe') = 0) and
              (CompareText(FullPath, UninstallerPath) <> 0) then
      begin
        SetArrayLength(AppExecutables, GetArrayLength(AppExecutables) + 1);
        AppExecutables[GetArrayLength(AppExecutables) - 1] := FullPath;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

// Opens a Restart Manager session and reports how many processes are using the
// application's executables, leaving the session open so the caller can shut them
// down. Returns -1 when the Restart Manager cannot answer.
//
// Every count opens its own session on purpose: the affected-application list is
// cached for the life of a session, so counting again on a handle already used
// keeps returning the number from before a shutdown, however long the wait.
function OpenSessionAndCount(var Session: Cardinal): Integer;
var
  KeyBuffer: String;
  ListResult, Needed, Supplied, RebootReasons, FileNamePtr: Cardinal;
  Index: Integer;
begin
  Result := -1;
  Session := 0;
  KeyBuffer := RM_SESSION_KEY_BUFFER;
  if RmStartSession(Session, 0, Cardinal(CastStringToInteger(KeyBuffer))) <> RM_SUCCESS then
  begin
    Session := 0;
    Exit;
  end;
  for Index := 0 to GetArrayLength(AppExecutables) - 1 do
  begin
    FileNamePtr := Cardinal(CastStringToInteger(AppExecutables[Index]));
    if RmRegisterResources(Session, 1, FileNamePtr, 0, 0, 0, 0) <> RM_SUCCESS then
    begin
      RmEndSession(Session);
      Session := 0;
      Exit;
    end;
  end;
  Needed := 0;
  Supplied := 0;
  RebootReasons := 0;
  // Counting call: a null rgAffectedApps makes RmGetList report the number of
  // affected processes in pnProcInfoNeeded instead of filling a structure array,
  // which is the whole of what this check needs.
  ListResult := RmGetList(Session, Needed, Supplied, 0, RebootReasons);
  if (ListResult = RM_SUCCESS) or (ListResult = RM_MORE_DATA) then
    Result := Needed
  else
  begin
    RmEndSession(Session);
    Session := 0;
  end;
end;

function CountRunningProcesses: Integer;
var
  Session: Cardinal;
begin
  Result := OpenSessionAndCount(Session);
  if Session <> 0 then
    RmEndSession(Session);
end;

// Asks the Restart Manager to close the processes, and reports whether the
// installation directory is free afterwards.
//
// No wait of our own belongs here. RmShutdown blocks until the processes have gone
// or its own timeout expires — that is what its progress-callback parameter is for
// — so the count taken straight after it already reflects the outcome, including
// for an application that spends seconds saving state on its way out.
function CloseRunningProcesses: Boolean;
var
  Session: Cardinal;
begin
  if OpenSessionAndCount(Session) > 0 then
    RmShutdown(Session, RM_SHUTDOWN_GRACEFUL, 0);
  if Session <> 0 then
    RmEndSession(Session);
  Result := CountRunningProcesses <= 0;
end;

function InitializeUninstall(): Boolean;
var
  ButtonLabels: TArrayOfString;
  Answer: Integer;
  CloseAttemptFailed: Boolean;
begin
  Result := True;
  SetArrayLength(AppExecutables, 0);
  CollectAppExecutables(ExpandConstant('{app}'), ExpandConstant('{uninstallexe}'));
  if GetArrayLength(AppExecutables) = 0 then
    Exit;

  SetArrayLength(ButtonLabels, 3);
  ButtonLabels[0] := CustomMessage('AppRunningCloseAndRetry');
  ButtonLabels[1] := CustomMessage('AppRunningRetry');
  ButtonLabels[2] := CustomMessage('AppRunningCancel');
  CloseAttemptFailed := False;

  repeat
    // A negative count means the Restart Manager could not answer. The uninstall
    // is not blocked on a check that failed to run: it proceeds as it did before
    // this check existed.
    if CountRunningProcesses <= 0 then
      Exit;

    if UninstallSilent then
    begin
      // No dialog can be shown. Unattended deployment is what runs a silent
      // uninstall, and stopping here would fail that deployment with nothing on
      // screen to explain it, so the application is closed and the uninstall
      // continues.
      Result := CloseRunningProcesses;
      Exit;
    end;

    if CloseAttemptFailed then
      Answer := TaskDialogMsgBox(CustomMessage('AppRunningCloseFailedCaption'),
        CustomMessage('AppRunningCloseFailedText'), mbError, MB_YESNOCANCEL,
        ButtonLabels, 0)
    else
      Answer := TaskDialogMsgBox(CustomMessage('AppRunningCaption'),
        CustomMessage('AppRunningText'), mbConfirmation, MB_YESNOCANCEL,
        ButtonLabels, 0);

    case Answer of
      IDYES:
        CloseAttemptFailed := not CloseRunningProcesses;
      IDNO:
        CloseAttemptFailed := False;
    else
      // IDCANCEL, and 0 when the dialog could not be shown at all. The else part
      // of a case statement takes a single statement here, hence the begin block.
      begin
        Result := False;
        Exit;
      end;
    end;
  until False;
end;

// Rewrite the user PATH: remove every entry equal to TargetPath (case-insensitive),
// then optionally append a single fresh entry at the end.
procedure UpdateUserPath(TargetPath: string; AddEntry: Boolean);
var
  OrigPath: string;
  TargetUpper: string;
  Remaining: string;
  Entry: string;
  Rebuilt: string;
  SemiPos: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', OrigPath) then
    OrigPath := '';

  TargetUpper := Uppercase(TargetPath);
  Remaining := OrigPath;
  Rebuilt := '';

  while Length(Remaining) > 0 do begin
    SemiPos := Pos(';', Remaining);
    if SemiPos > 0 then begin
      Entry := Copy(Remaining, 1, SemiPos - 1);
      Remaining := Copy(Remaining, SemiPos + 1, MaxInt);
    end else begin
      Entry := Remaining;
      Remaining := '';
    end;

    if (Entry <> '') and (Uppercase(Entry) <> TargetUpper) then begin
      if Rebuilt <> '' then
        Rebuilt := Rebuilt + ';';
      Rebuilt := Rebuilt + Entry;
    end;
  end;

  if AddEntry then begin
    if Rebuilt <> '' then
      Rebuilt := Rebuilt + ';';
    Rebuilt := Rebuilt + TargetPath;
  end;

  RegWriteExpandStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Rebuilt);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
    UpdateUserPath(ExpandConstant('{app}'), True);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    UpdateUserPath(ExpandConstant('{app}'), False);
end;
