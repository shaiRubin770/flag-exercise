<#
.SYNOPSIS
  Publishes both FlagExercise services and compiles the Inno Setup installer.

.DESCRIPTION
  Steps performed:
    1. dotnet publish FlagExercise.TxService  ->  installer\publish\Tx\
    2. dotnet publish FlagExercise.RxService  ->  installer\publish\Rx\
    3. ISCC.exe FlagExercise.iss              ->  installer\dist\FlagExercise-Setup-1.0.0.exe

  Run this script from any directory; it locates the repo root automatically.

.PARAMETER SelfContained
  $true  (default) - bundles the .NET 8 runtime; no .NET install needed on target.
  $false           - framework-dependent; requires .NET 8 runtime on target (~smaller).

.PARAMETER SkipCompile
  Skip running ISCC.exe even if Inno Setup is installed.
  Useful if you only want to regenerate the publish output.

.PARAMETER Configuration
  Build configuration. Default: Release.

.EXAMPLES
  # Standard build (self-contained, produces setup.exe)
  powershell -ExecutionPolicy Bypass -File .\build-installer.ps1

  # Framework-dependent build (target machine must have .NET 8 runtime)
  powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -SelfContained $false

  # Publish only, skip ISCC compilation
  powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -SkipCompile
#>

[CmdletBinding()]
param(
    [bool]  $SelfContained   = $true,
    [switch]$SkipCompile,
    [string]$Configuration   = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir  = $PSScriptRoot
$repoRoot   = (Resolve-Path (Join-Path $scriptDir "..")).Path
$publishDir = Join-Path $scriptDir "publish"
$issFile    = Join-Path $scriptDir "FlagExercise.iss"

# ---- Verify .NET SDK -------------------------------------------------------
$dotnetVer = & dotnet --version 2>$null
if (-not $dotnetVer) {
    throw "dotnet SDK not found on PATH. Download from https://dotnet.microsoft.com/download/dotnet/8.0"
}
Write-Host "Using .NET SDK $dotnetVer"

# ---- Publish both services -------------------------------------------------
foreach ($role in @("Tx", "Rx")) {
    $proj = Join-Path $repoRoot "src\FlagExercise.${role}Service\FlagExercise.${role}Service.csproj"
    if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

    $dest = Join-Path $publishDir $role
    Write-Host ""
    Write-Host "[$role] Publishing to: $dest"
    Write-Host "       Self-contained : $SelfContained"

    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null

    $args = @(
        "publish", $proj,
        "-c", $Configuration,
        "-r", "win-x64",
        "--self-contained", ($SelfContained.ToString().ToLower()),
        "-o", $dest
    )
    & dotnet @args | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $role (exit $LASTEXITCODE)" }

    $exe = Join-Path $dest "FlagExercise.${role}Service.exe"
    if (-not (Test-Path $exe)) { throw "Expected output not found: $exe" }
    Write-Host "[$role] OK -> $exe"
}

# ---- Compile with Inno Setup -----------------------------------------------
if ($SkipCompile) {
    Write-Host ""
    Write-Host "-SkipCompile set - skipping ISCC compilation." -ForegroundColor Yellow
    exit 0
}

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host ""
    Write-Warning "Inno Setup 6 not found on this machine."
    Write-Warning "Download (free): https://jrsoftware.org/issetup.php"
    Write-Warning ""
    Write-Warning "Once installed, compile manually with:"
    Write-Warning "  `"${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe`" `"$issFile`""
    Write-Warning "OR re-run this script."
    exit 0
}

Write-Host ""
Write-Host "Compiling installer: $issFile"
Write-Host "ISCC: $iscc"

$distDir = Join-Path $scriptDir "dist"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

& $iscc $issFile | Out-Host
if ($LASTEXITCODE -ne 0) { throw "ISCC.exe compilation failed (exit $LASTEXITCODE)" }

$outputExe = Get-ChildItem -Path $distDir -Filter "FlagExercise-Setup-*.exe" |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

Write-Host ""
if ($outputExe) {
    Write-Host "Installer ready:" -ForegroundColor Green
    Write-Host "  $($outputExe.FullName)" -ForegroundColor Green
    Write-Host "  Size: $([math]::Round($outputExe.Length / 1MB, 1)) MB"
} else {
    Write-Host "Compilation succeeded. Check installer\dist\ for the output file." -ForegroundColor Green
}
Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
