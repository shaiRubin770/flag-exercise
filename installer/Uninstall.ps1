<#
.SYNOPSIS
  Stops and removes one or both FlagExercise services and their installed files.
#>
[CmdletBinding()]
param(
  [ValidateSet("Tx","Rx")] [string]$Role,
  [string]$InstallRoot = "$Env:ProgramFiles\FlagExercise",
  [switch]$KeepData
)

function Require-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $p  = New-Object Security.Principal.WindowsPrincipal($id)
  if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script in an elevated (Administrator) PowerShell."
  }
}

function Remove-Role([string]$r) {
  $svcName = "FlagExercise.$r"
  if (Get-Service -Name $svcName -ErrorAction SilentlyContinue) {
    Write-Host "[$r] Stopping & removing service..."
    sc.exe stop $svcName | Out-Null
    Start-Sleep -Seconds 2
    sc.exe delete $svcName | Out-Null
    Start-Sleep -Seconds 1
  } else {
    Write-Host "[$r] Service not present."
  }

  $dir = Join-Path $InstallRoot $r
  if (Test-Path $dir) {
    Write-Host "[$r] Removing files at $dir ..."
    Remove-Item $dir -Recurse -Force
  }

  try { netsh advfirewall firewall delete rule name="FlagExercise-$r" | Out-Null } catch {}

  if (-not $KeepData) {
    $data = Join-Path $Env:ProgramData "FlagExercise\$r"
    if (Test-Path $data) {
      Write-Host "[$r] Removing config & logs at $data ..."
      Remove-Item $data -Recurse -Force
    }
  }
}

Require-Admin

if (-not $Role) {
  Write-Host "Which service do you want to remove from THIS machine?"
  Write-Host "  1) T(x)"
  Write-Host "  2) R(x)"
  $choice = Read-Host "Enter 1 or 2"
  switch ($choice) {
    "1" { $Role = "Tx" }
    "2" { $Role = "Rx" }
    default { throw "Invalid choice. Please enter 1 or 2." }
  }
}

switch ($Role) {
  "Tx" { Remove-Role "Tx" }
  "Rx" { Remove-Role "Rx" }
}
Write-Host "Uninstall complete." -ForegroundColor Cyan
