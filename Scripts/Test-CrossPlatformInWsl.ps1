#Requires -Version 5.1
<##
.SYNOPSIS
  Build and test the portable Linux surface from a Windows checkout through WSL.

.DESCRIPTION
  This verifies the actual Linux TFM and Linux host behavior without changing the
  Windows machine's SDK or application state. A WSL distribution with .NET 10
  installed is required; the script never installs a distribution automatically.
#>
[CmdletBinding()]
param(
    [string] $Distro = '',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$wslCommand = Get-Command wsl.exe -ErrorAction SilentlyContinue
if ($null -eq $wslCommand) {
    throw 'WSL is not installed. Install it with: wsl --install -d Ubuntu'
}

function Remove-WslOutputPadding {
    param([string] $Value)

    return ($Value -replace "`0", '').Trim()
}

$rawDistros = ''
$wslListExitCode = 0
try {
    $rawDistros = (& $wslCommand.Source --list --quiet 2>&1 | Out-String)
    $wslListExitCode = $LASTEXITCODE
}
catch {
    # WSL reports "install a distribution" through stderr as a native
    # PowerShell error when no distribution exists. Treat that as a clean
    # prerequisite failure below so the user gets an actionable message.
    $wslListExitCode = 1
}

if ($wslListExitCode -ne 0) {
    throw 'No WSL distribution is installed. Install one with: wsl --install -d Ubuntu; then rerun this script.'
}

$distros = @(
    $rawDistros -split "`r?`n" |
        ForEach-Object { Remove-WslOutputPadding $_ } |
        Where-Object { $_ -match '^[A-Za-z0-9][A-Za-z0-9._-]*$' }
)

if ([string]::IsNullOrWhiteSpace($Distro)) {
    $Distro = $distros |
        Where-Object { $_ -notmatch '^docker-' } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($Distro)) {
    throw 'No WSL distribution is installed. Install one with: wsl --install -d Ubuntu; then rerun this script.'
}

if ($distros -notcontains $Distro) {
    throw "WSL distribution '$Distro' was not found. Installed distributions: $($distros -join ', ')"
}

# wsl.exe forwards backslashes as escape characters when it invokes a Linux
# command. Double them before calling wslpath so OneDrive and other Windows
# path segments are preserved verbatim.
$repoPathForWsl = $repoRoot.Replace('\', '\\')
$repoInWsl = Remove-WslOutputPadding (& $wslCommand.Source -d $Distro -- wslpath -a -- $repoPathForWsl | Out-String)
if ([string]::IsNullOrWhiteSpace($repoInWsl)) {
    throw "Could not convert repository path '$repoRoot' into a WSL path."
}

$bashScript = @'
set -euo pipefail
repo="$(printf '%s' "$1" | base64 -d)"
configuration="$(printf '%s' "$2" | base64 -d)"
cd "$repo"

echo "WSL distribution: $(cat /etc/os-release | sed -n 's/^PRETTY_NAME=//p' | tr -d '"')"
echo "dotnet: $(dotnet --version)"

dotnet restore Tests/CrossPlatform/UniversalDeviceToolkit.CrossPlatform.Tests.csproj \
  --locked-mode --force-evaluate -p:EnableWindowsTargeting=true
dotnet build Platforms/Linux/UniversalDeviceToolkit.Platform.Linux.csproj --configuration "$configuration" --no-restore
dotnet build Tests/CrossPlatform/UniversalDeviceToolkit.CrossPlatform.Tests.csproj --configuration "$configuration" --no-restore
dotnet test Tests/CrossPlatform/UniversalDeviceToolkit.CrossPlatform.Tests.csproj \
  --configuration "$configuration" --no-build \
  --logger "trx;LogFileName=wsl-linux-test-results.trx"

status="$(dotnet run --project Apps/CrossPlatformCLI/UniversalDeviceToolkit.CrossPlatform.csproj --configuration "$configuration" --no-build -- status)"
echo "$status" | grep -qi 'cross-platform diagnostics'

hardware="$(dotnet run --project Apps/CrossPlatformCLI/UniversalDeviceToolkit.CrossPlatform.csproj --configuration "$configuration" --no-build -- hardware)"
echo "$hardware" | grep -qi 'Hardware identity'

json="$(dotnet run --project Apps/CrossPlatformCLI/UniversalDeviceToolkit.CrossPlatform.csproj --configuration "$configuration" --no-build -- json)"
echo "$json" | grep -q 'Universal Device Toolkit'

echo 'WSL Linux verification passed.'
'@

Write-Host "Running portable verification in WSL distribution '$Distro'..." -ForegroundColor Cyan
$bashScriptBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($bashScript))
$repoArgumentBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($repoInWsl))
$configurationBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($Configuration))
$bootstrap = "printf '%s' '$bashScriptBase64' | base64 -d | bash -s -- '$repoArgumentBase64' '$configurationBase64'"
& $wslCommand.Source -d $Distro -- bash -lc $bootstrap
if ($LASTEXITCODE -ne 0) {
    throw "WSL verification failed with exit code $LASTEXITCODE."
}
