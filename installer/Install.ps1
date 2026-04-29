<#
.SYNOPSIS
  Installs the FlagExercise service on this machine - either Tx or Rx.

.DESCRIPTION
  This is the full installation. The user MUST choose one role (Tx OR Rx).
  A single machine never runs both - that is the whole point of the exercise:
  one machine sends, another machine receives.

  The script:
    1. Asks (or accepts -Role) whether this is the Tx or the Rx side.
    2. Builds and publishes the chosen service to %ProgramFiles%\FlagExercise\<Role>.
    3. Registers it as a Windows Service via sc.exe and starts it.
    4. Opens the firewall on its UI port.

  Service logs are written to %ProgramData%\FlagExercise\<Role>\logs\.

.EXAMPLES
  # Interactive (recommended)
  powershell -ExecutionPolicy Bypass -File .\Install.ps1

  # Non-interactive
  powershell -ExecutionPolicy Bypass -File .\Install.ps1 -Role Tx
  powershell -ExecutionPolicy Bypass -File .\Install.ps1 -Role Rx
#>

[CmdletBinding()]
param(
  [ValidateSet("Tx","Rx")] [string]$Role,
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")),
  [string]$InstallRoot = "$Env:ProgramFiles\FlagExercise",
  [int]$TxPort = 5081,
  [int]$RxPort = 5082
)

function Require-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $p  = New-Object Security.Principal.WindowsPrincipal($id)
  if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script in an elevated (Administrator) PowerShell."
  }
}

function Require-Dotnet {
  $v = & dotnet --version 2>$null
  if (-not $v) { throw "The .NET 8 SDK is required and was not found on PATH." }
  Write-Host "Using .NET SDK $v"
}

function Install-Role([string]$r, [int]$port) {
  $proj = Join-Path $RepoRoot "src\FlagExercise.${r}Service\FlagExercise.${r}Service.csproj"
  if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

  $dest = Join-Path $InstallRoot $r
  Write-Host "[$r] Publishing to $dest ..."
  if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $dest | Out-Null

  & dotnet publish $proj -c Release -r win-x64 --self-contained false -o $dest | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "publish failed for $r" }

  $exe = Join-Path $dest "FlagExercise.${r}Service.exe"
  if (-not (Test-Path $exe)) { throw "Expected exe missing: $exe" }

  $svcName    = "FlagExercise.$r"
  $svcDisplay = "FlagExercise $r Service"
  $url        = "http://localhost:$port"

  # Stop & delete any previous installation (idempotent).
  if (Get-Service -Name $svcName -ErrorAction SilentlyContinue) {
    Write-Host "[$r] Stopping existing service..."
    sc.exe stop $svcName | Out-Null
    Start-Sleep -Seconds 2
    sc.exe delete $svcName | Out-Null
    Start-Sleep -Seconds 2
  }

  Write-Host "[$r] Registering service '$svcName' ..."
  $bin = '"' + $exe + '"'
  sc.exe create $svcName binPath= $bin DisplayName= $svcDisplay start= auto | Out-Null
  sc.exe description $svcName "FlagExercise $r service. UI at $url. Logs in %ProgramData%\FlagExercise\$r\logs." | Out-Null
  sc.exe failure     $svcName reset= 60 actions= restart/5000/restart/5000/restart/15000 | Out-Null

  # Per-machine env var so the service binds to the chosen URL.
  $envVar = "FLAGEX_${r}_URL".ToUpper()
  [Environment]::SetEnvironmentVariable($envVar, $url, "Machine")

  # Open the firewall (best effort).
  try {
    netsh advfirewall firewall delete rule name="FlagExercise-$r" | Out-Null
    netsh advfirewall firewall add    rule name="FlagExercise-$r" dir=in action=allow protocol=TCP localport=$port | Out-Null
  } catch {}

  Write-Host "[$r] Starting service..."
  sc.exe start $svcName | Out-Null
  Start-Sleep -Seconds 2
  sc.exe query $svcName | Out-Host

  Write-Host ""
  Write-Host "[$r] Installed. Open the UI at: $url" -ForegroundColor Green
}

# ---------------- main ----------------
Require-Admin
Require-Dotnet

if (-not $Role) {
  Write-Host ""
  Write-Host "Which side do you want to install on THIS machine?"
  Write-Host "  1) T(x) - Source / sends files"
  Write-Host "  2) R(x) - Destination / receives and deletes files"
  $choice = Read-Host "Enter 1 or 2"
  switch ($choice) {
    "1" { $Role = "Tx" }
    "2" { $Role = "Rx" }
    default { throw "Invalid choice. Please enter 1 or 2." }
  }
}

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null

switch ($Role) {
  "Tx" { Install-Role "Tx" $TxPort }
  "Rx" { Install-Role "Rx" $RxPort }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
if ($Role -eq "Tx") { Write-Host "Tx UI -> http://localhost:$TxPort" }
if ($Role -eq "Rx") { Write-Host "Rx UI -> http://localhost:$RxPort" }
