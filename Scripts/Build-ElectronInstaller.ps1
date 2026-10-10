# Compatibility entry point for scripts using the former Windows builder name.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$InstallerOutput = 'BuildInstaller',
    [string]$PayloadOutput = 'BuildInstallerPayload',
    [switch]$PreparePayloadsOnly,
    [switch]$PrepareInstallerShellOnly,
    [switch]$PackagePreparedPayloads,
    [switch]$SkipAppCheck
)
if ($PrepareInstallerShellOnly) {
    Write-Host 'The WebView2 installer shell is already prepared with its application payload.'
    return
}
& (Join-Path $PSScriptRoot 'Build-WebView2Installer.ps1') -Version $Version `
    -InstallerOutput $InstallerOutput -PayloadOutput $PayloadOutput `
    -PreparePayloadsOnly:$PreparePayloadsOnly -PackagePreparedPayloads:$PackagePreparedPayloads `
    -SkipAppCheck:$SkipAppCheck
