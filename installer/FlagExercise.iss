; ---------------------------------------------------------------------------
;  FlagExercise — Inno Setup 6 installer script
;
;  How to build:
;    1. Run build-installer.ps1  (publishes binaries + compiles this script)
;  OR manually:
;    1. dotnet publish ... -o installer\publish\Tx
;    2. dotnet publish ... -o installer\publish\Rx
;    3. ISCC.exe installer\FlagExercise.iss
;
;  Output:  installer\dist\FlagExercise-Setup-1.0.0.exe
; ---------------------------------------------------------------------------

#define MyAppName    "FlagExercise"
#define MyAppVersion "1.0.0"
#define TxPort       "5081"
#define RxPort       "5082"
#define TxSvc        "FlagExercise.Tx"
#define RxSvc        "FlagExercise.Rx"

; ---------------------------------------------------------------------------
[Setup]
; ---------------------------------------------------------------------------
AppId={{C3D7E912-8A54-4F6B-9D3E-1B2C4A5F6D7E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=FlagExercise
DefaultDirName={autopf}\FlagExercise
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; 64-bit Windows only (matches win-x64 publish)
ArchitecturesInstallIn64BitMode=x64compatible
; Minimum OS: Windows 10 / Server 2016
MinVersion=10.0
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Don't close running apps automatically (services are handled in [Run])
CloseApplications=no
; Output
OutputDir=dist
OutputBaseFilename=FlagExercise-Setup-{#MyAppVersion}
UninstallDisplayName={#MyAppName}

; ---------------------------------------------------------------------------
[Languages]
; ---------------------------------------------------------------------------
Name: "english"; MessagesFile: "compiler:Default.isl"

; ---------------------------------------------------------------------------
[Files]
; Only the selected role's files are installed.
; ---------------------------------------------------------------------------
Source: "publish\Tx\*"; DestDir: "{app}\Tx"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsTxRole
Source: "publish\Rx\*"; DestDir: "{app}\Rx"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsRxRole

; ---------------------------------------------------------------------------
[Registry]
; Record which role was installed so the uninstaller knows what to remove.
; ---------------------------------------------------------------------------
Root: HKLM; Subkey: "SOFTWARE\FlagExercise"; ValueType: string; ValueName: "InstalledRole"; ValueData: "Tx"; Check: IsTxRole; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\FlagExercise"; ValueType: string; ValueName: "InstalledRole"; ValueData: "Rx"; Check: IsRxRole; Flags: uninsdeletekey

; Per-machine URL environment variables (same as Install.ps1).
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "FLAGEX_TX_URL"; ValueData: "http://localhost:{#TxPort}"; Check: IsTxRole; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "FLAGEX_RX_URL"; ValueData: "http://localhost:{#RxPort}"; Check: IsRxRole; Flags: uninsdeletevalue

; ---------------------------------------------------------------------------
[Run]
; Executed after files are copied, in order.
; ---------------------------------------------------------------------------

; --- 1. Stop & delete any previous installation of the chosen role ---
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#TxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden; Check: IsTxRole; StatusMsg: "Removing previous Tx service (if any)..."
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#RxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden; Check: IsRxRole; StatusMsg: "Removing previous Rx service (if any)..."

; --- 2. Create Windows Service ---
Filename: "{sys}\sc.exe"; Parameters: "create {#TxSvc} binPath= ""{app}\Tx\FlagExercise.TxService.exe"" DisplayName= ""FlagExercise Tx Service"" start= auto"; Flags: runhidden; Check: IsTxRole; StatusMsg: "Registering Tx service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#TxSvc} ""FlagExercise Tx service. UI at http://localhost:{#TxPort}. Logs in %ProgramData%\FlagExercise\Tx\logs."""; Flags: runhidden; Check: IsTxRole
Filename: "{sys}\sc.exe"; Parameters: "failure {#TxSvc} reset= 60 actions= restart/5000/restart/5000/restart/15000"; Flags: runhidden; Check: IsTxRole

Filename: "{sys}\sc.exe"; Parameters: "create {#RxSvc} binPath= ""{app}\Rx\FlagExercise.RxService.exe"" DisplayName= ""FlagExercise Rx Service"" start= auto"; Flags: runhidden; Check: IsRxRole; StatusMsg: "Registering Rx service..."
Filename: "{sys}\sc.exe"; Parameters: "description {#RxSvc} ""FlagExercise Rx service. UI at http://localhost:{#RxPort}. Logs in %ProgramData%\FlagExercise\Rx\logs."""; Flags: runhidden; Check: IsRxRole
Filename: "{sys}\sc.exe"; Parameters: "failure {#RxSvc} reset= 60 actions= restart/5000/restart/5000/restart/15000"; Flags: runhidden; Check: IsRxRole

; --- 3. Open Windows Firewall ---
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Tx"""; Flags: runhidden; Check: IsTxRole
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""FlagExercise-Tx"" dir=in action=allow protocol=TCP localport={#TxPort}"; Flags: runhidden; Check: IsTxRole; StatusMsg: "Configuring firewall for Tx..."

Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Rx"""; Flags: runhidden; Check: IsRxRole
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""FlagExercise-Rx"" dir=in action=allow protocol=TCP localport={#RxPort}"; Flags: runhidden; Check: IsRxRole; StatusMsg: "Configuring firewall for Rx..."

; --- 4. Start the service ---
Filename: "{sys}\sc.exe"; Parameters: "start {#TxSvc}"; Flags: runhidden; Check: IsTxRole; StatusMsg: "Starting Tx service..."
Filename: "{sys}\sc.exe"; Parameters: "start {#RxSvc}"; Flags: runhidden; Check: IsRxRole; StatusMsg: "Starting Rx service..."

; ---------------------------------------------------------------------------
[UninstallRun]
; ---------------------------------------------------------------------------

; Stop & delete whichever service(s) are present (errors are suppressed).
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#TxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""$s='{#RxSvc}'; if (Get-Service $s -EA SilentlyContinue) {{ sc.exe stop $s | Out-Null; Start-Sleep 2; sc.exe delete $s | Out-Null }}"""; Flags: runhidden

; Remove firewall rules.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Tx"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""FlagExercise-Rx"""; Flags: runhidden

; ---------------------------------------------------------------------------
[Code]
// ---------------------------------------------------------------------------
var
  RolePage: TInputOptionWizardPage;

{ Returns True when the Tx radio button is selected. }
function IsTxRole: Boolean;
begin
  Result := (RolePage <> nil) and RolePage.Values[0];
end;

{ Returns True when the Rx radio button is selected. }
function IsRxRole: Boolean;
begin
  Result := (RolePage <> nil) and RolePage.Values[1];
end;

{ Create the role-selection page immediately after the directory page. }
procedure InitializeWizard;
begin
  RolePage := CreateInputOptionPage(
    wpSelectDir,
    'Select Role',
    'Choose which role to install on this machine',
    'One role per machine. Select the role for THIS machine, then click Next.',
    True,   { ExclusiveOptions = True  →  radio buttons }
    False   { not a ListBox }
  );
  RolePage.Add('T(x)  —  Sender   [UI will open at http://localhost:{#TxPort}]');
  RolePage.Add('R(x)  —  Receiver [UI will open at http://localhost:{#RxPort}]');
  RolePage.Values[0] := True;   { default: Tx }
end;

{ Prevent advancing past the role page without a selection. }
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

{ Customize the "Ready to Install" summary. }
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
