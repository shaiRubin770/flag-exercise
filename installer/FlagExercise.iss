#define MyAppName    "FlagExercise"
#define MyAppVersion "1.0.0"
#define TxPort       "5081"
#define RxPort       "5082"
#define TxSvc        "FlagExercise.Tx"
#define RxSvc        "FlagExercise.Rx"

[Setup]
AppId={{C3D7E912-8A54-4F6B-9D3E-1B2C4A5F6D7E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=FlagExercise
DefaultDirName={autopf}\FlagExercise
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
OutputDir=dist
OutputBaseFilename=FlagExercise-Setup-{#MyAppVersion}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut to the FlagExercise dashboard"; GroupDescription: "Additional shortcuts:"

[Icons]
Name: "{autodesktop}\FlagExercise (Tx)"; Filename: "http://localhost:{#TxPort}"; IconFilename: "{app}\Tx\FlagExercise.TxService.exe"; Comment: "Open the FlagExercise T(x) dashboard"; Tasks: desktopicon; Check: IsTxRole
Name: "{autodesktop}\FlagExercise (Rx)"; Filename: "http://localhost:{#RxPort}"; IconFilename: "{app}\Rx\FlagExercise.RxService.exe"; Comment: "Open the FlagExercise R(x) dashboard"; Tasks: desktopicon; Check: IsRxRole

Name: "{autoprograms}\FlagExercise\FlagExercise (Tx) Dashboard"; Filename: "http://localhost:{#TxPort}"; IconFilename: "{app}\Tx\FlagExercise.TxService.exe"; Check: IsTxRole
Name: "{autoprograms}\FlagExercise\FlagExercise (Rx) Dashboard"; Filename: "http://localhost:{#RxPort}"; IconFilename: "{app}\Rx\FlagExercise.RxService.exe"; Check: IsRxRole
Name: "{autoprograms}\FlagExercise\Uninstall FlagExercise"; Filename: "{uninstallexe}"

[Files]
Source: "publish\Tx\*"; DestDir: "{app}\Tx"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsTxRole
Source: "publish\Rx\*"; DestDir: "{app}\Rx"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsRxRole

[Registry]
Root: HKLM; Subkey: "SOFTWARE\FlagExercise"; ValueType: string; ValueName: "InstalledRole"; ValueData: "Tx"; Check: IsTxRole; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\FlagExercise"; ValueType: string; ValueName: "InstalledRole"; ValueData: "Rx"; Check: IsRxRole; Flags: uninsdeletekey

Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "FLAGEX_TX_URL"; ValueData: "http://localhost:{#TxPort}"; Check: IsTxRole; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "FLAGEX_RX_URL"; ValueData: "http://localhost:{#RxPort}"; Check: IsRxRole; Flags: uninsdeletevalue

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#TxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden; Check: IsTxRole; StatusMsg: "Removing previous Tx service (if any)..."
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#RxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden; Check: IsRxRole; StatusMsg: "Removing previous Rx service (if any)..."

Filename: "{sys}\sc.exe"; Parameters: "create {#TxSvc} binPath= ""{app}\Tx\FlagExercise.TxService.exe"" DisplayName= ""FlagExercise Tx Service"" start= auto"; Flags: runhidden; Check: IsTxRole; StatusMsg: "Registering Tx service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#TxSvc} ""FlagExercise Tx service. UI at http://localhost:{#TxPort}. Logs in %ProgramData%\FlagExercise\Tx\logs."""; Flags: runhidden; Check: IsTxRole
Filename: "{sys}\sc.exe"; Parameters: "failure {#TxSvc} reset= 60 actions= restart/5000/restart/5000/restart/15000"; Flags: runhidden; Check: IsTxRole

Filename: "{sys}\sc.exe"; Parameters: "create {#RxSvc} binPath= ""{app}\Rx\FlagExercise.RxService.exe"" DisplayName= ""FlagExercise Rx Service"" start= auto"; Flags: runhidden; Check: IsRxRole; StatusMsg: "Registering Rx service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#RxSvc} ""FlagExercise Rx service. UI at http://localhost:{#RxPort}. Logs in %ProgramData%\FlagExercise\Rx\logs."""; Flags: runhidden; Check: IsRxRole
Filename: "{sys}\sc.exe"; Parameters: "failure {#RxSvc} reset= 60 actions= restart/5000/restart/5000/restart/15000"; Flags: runhidden; Check: IsRxRole

Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Tx"""; Flags: runhidden; Check: IsTxRole
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""FlagExercise-Tx"" dir=in action=allow protocol=TCP localport={#TxPort}"; Flags: runhidden; Check: IsTxRole; StatusMsg: "Configuring firewall for Tx..."

Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Rx"""; Flags: runhidden; Check: IsRxRole
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""FlagExercise-Rx"" dir=in action=allow protocol=TCP localport={#RxPort}"; Flags: runhidden; Check: IsRxRole; StatusMsg: "Configuring firewall for Rx..."

Filename: "{sys}\sc.exe"; Parameters: "start {#TxSvc}"; Flags: runhidden; Check: IsTxRole; StatusMsg: "Starting Tx service..."
Filename: "{sys}\sc.exe"; Parameters: "start {#RxSvc}"; Flags: runhidden; Check: IsRxRole; StatusMsg: "Starting Rx service..."

; Offer to open the dashboard in the default browser when setup finishes.
Filename: "http://localhost:{#TxPort}"; Description: "Open the FlagExercise T(x) dashboard now"; Flags: postinstall nowait shellexec skipifsilent; Check: IsTxRole
Filename: "http://localhost:{#RxPort}"; Description: "Open the FlagExercise R(x) dashboard now"; Flags: postinstall nowait shellexec skipifsilent; Check: IsRxRole

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#TxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#RxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden

Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Tx"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Rx"""; Flags: runhidden

[Code]
var
  RolePage: TInputOptionWizardPage;

function IsTxRole: Boolean;
begin
  Result := (RolePage <> nil) and RolePage.Values[0];
end;

function IsRxRole: Boolean;
begin
  Result := (RolePage <> nil) and RolePage.Values[1];
end;

procedure InitializeWizard;
begin
  RolePage := CreateInputOptionPage(
    wpSelectDir,
    'Select Role',
    'Choose which role to install on this machine',
    'One role per machine. Select the role for THIS machine, then click Next.',
    True,
    False
  );
  RolePage.Add('T(x)  —  Sender   [UI will open at http://localhost:{#TxPort}]');
  RolePage.Add('R(x)  —  Receiver [UI will open at http://localhost:{#RxPort}]');
  RolePage.Values[0] := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (RolePage <> nil) and (CurPageID = RolePage.ID) then
    if not RolePage.Values[0] and not RolePage.Values[1] then
    begin
      MsgBox('Please select a role before continuing.', mbError, MB_OK);
      Result := False;
    end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
  MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  RoleLine: String;
begin
  if IsTxRole then
    RoleLine := 'T(x)  —  Sender  (UI: http://localhost:{#TxPort})'
  else
    RoleLine := 'R(x)  —  Receiver  (UI: http://localhost:{#RxPort})';

  Result :=
    'Role to install:' + NewLine + Space + RoleLine + NewLine + NewLine +
    MemoDirInfo;
end;
