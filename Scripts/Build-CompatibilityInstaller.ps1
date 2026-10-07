[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')][string]$Version,
    [string]$InstallerOutput = 'BuildInstaller',
    [string]$PayloadOutput = 'BuildInstallerPayload',
    [switch]$PreparePayloadsOnly,
    [switch]$PackagePreparedPayloads
)
$ErrorActionPreference = 'Stop'
if ($PreparePayloadsOnly -and $PackagePreparedPayloads) { throw 'Choose one build phase.' }
$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'Apps/Electron'
$arguments = @((Join-Path $project 'scripts/build-compatibility-installer.mjs'), '--version', $Version,
    '--payload-directory', (Join-Path $repository "$PayloadOutput/compatibility"),
    '--output-directory', (Join-Path $repository $InstallerOutput))
if (-not $PackagePreparedPayloads) {
    & npm.cmd --prefix $project run build
    if ($LASTEXITCODE -ne 0) { throw 'Renderer build failed.' }
}
if ($PreparePayloadsOnly) { $arguments += '--prepare-only' }
if ($PackagePreparedPayloads) { $arguments += '--package-only' }
& node @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compatibility installer build failed.' }
