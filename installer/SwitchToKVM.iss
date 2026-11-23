#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif

#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{2984A483-6692-4021-8AD0-11CAFC0056CF}
AppName=SwitchToKVM
AppVersion={#AppVersion}
AppPublisher=SwitchToKVM
DefaultDirName={localappdata}\Programs\SwitchToKVM
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=SwitchToKVM-{#AppVersion}-setup
SetupIconFile=..\src\SwitchToKVM.App\Assets\icon.ico
UninstallDisplayIcon={app}\SwitchToKVM.exe
UninstallDisplayName=SwitchToKVM
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
ChangesEnvironment=yes

[Tasks]
Name: "startup"; Description: "Start SwitchToKVM when I sign in"
Name: "addtopath"; Description: "Add the stkvm command-line tool to PATH"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{userprograms}\SwitchToKVM"; Filename: "{app}\SwitchToKVM.exe"

[Registry]
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "SwitchToKVM"; ValueData: """{app}\SwitchToKVM.exe"""; Tasks: startup

[Run]
Filename: "{app}\SwitchToKVM.exe"; Description: "Start SwitchToKVM now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\stkvm.exe"; Parameters: "quit"; Flags: runhidden; RunOnceId: "QuitApp"

[Code]
const
  EnvironmentKey = 'Environment';

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Cli: String;
  ResultCode: Integer;
begin
  Cli := ExpandConstant('{app}\stkvm.exe');
  if FileExists(Cli) then
    Exec(Cli, 'quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure AddToPath(Dir: String);
var
  Paths: String;
begin
  if not RegQueryStringValue(HKCU, EnvironmentKey, 'Path', Paths) then
    Paths := '';
  if Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(Paths) + ';') > 0 then
    exit;
  if (Paths <> '') and (Copy(Paths, Length(Paths), 1) <> ';') then
    Paths := Paths + ';';
  RegWriteExpandStringValue(HKCU, EnvironmentKey, 'Path', Paths + Dir);
end;

procedure RemoveFromPath(Dir: String);
var
  Paths, Padded: String;
  P: Integer;
begin
  if not RegQueryStringValue(HKCU, EnvironmentKey, 'Path', Paths) then
    exit;
  Padded := ';' + Paths + ';';
  P := Pos(';' + Uppercase(Dir) + ';', Uppercase(Padded));
  if P = 0 then
    exit;
  Delete(Padded, P, Length(Dir) + 1);
  RegWriteExpandStringValue(HKCU, EnvironmentKey, 'Path', Copy(Padded, 2, Length(Padded) - 2));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
    AddToPath(ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveFromPath(ExpandConstant('{app}'));
    RegDeleteValue(HKCU, '{#RunKey}', 'SwitchToKVM');
  end;
end;
