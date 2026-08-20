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

[Code]
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
