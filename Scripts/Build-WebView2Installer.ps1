[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,
    [string]$InstallerOutput = 'BuildInstaller',
    [string]$PayloadOutput = 'BuildInstallerPayload',
    [switch]$PreparePayloadsOnly,
    [switch]$PackagePreparedPayloads,
    [switch]$SkipAppCheck
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PreparePayloadsOnly -and $PackagePreparedPayloads) { throw 'Choose one build phase.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'Apps/Electron'
$output = [IO.Path]::GetFullPath((Join-Path $repoRoot $InstallerOutput))
$payload = [IO.Path]::GetFullPath((Join-Path $repoRoot "$PayloadOutput/full"))
Push-Location $project
try {
    if (-not $PackagePreparedPayloads) {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Renderer build failed.' }
    }
    $arguments = @('scripts/build-windows-installer.mjs', '--version', $Version,
        '--payload-directory', $payload, '--output-directory', $output, '--use-published-host')
    if ($PreparePayloadsOnly) { $arguments += '--prepare-only' }
    if ($PackagePreparedPayloads) { $arguments += '--package-only' }
    if ($SkipAppCheck) { $arguments += '--skip-app-check' }
    & node @arguments
    if ($LASTEXITCODE -ne 0) { throw 'WebView2 installer build failed.' }
    if (-not $PreparePayloadsOnly) {
        $installer = Join-Path $output "UniversalDeviceToolkitWebView2Setup-$Version.exe"
        $portable = Join-Path $output "UniversalDeviceToolkitWebView2-$Version-win-x64.zip"
        # Old Full/Online clients continue to find their expected asset names.
        # Both channels now install the same complete WebView2 application.
        Copy-Item -LiteralPath $installer -Destination (Join-Path $output 'UniversalDeviceToolkitSetup.exe') -Force
        Copy-Item -LiteralPath $installer -Destination (Join-Path $output 'UniversalDeviceToolkitOnlineSetup.exe') -Force
        foreach ($channel in @('Full', 'Online')) {
            Copy-Item -LiteralPath $portable -Destination (Join-Path $output "UniversalDeviceToolkit_v${Version}_${channel}_win-x64.zip") -Force
        }
    }
}
finally { Pop-Location }
