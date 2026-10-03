[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$InstallerSha256,

    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string]$PortableSha256,

    [string]$HashManifestPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string]$ReleaseDate,

    [string]$InstallerUrl,

    [string]$RootPath = '',

    [switch]$UpdatePublishedScoopManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$null = [DateTime]::ParseExact(
    $ReleaseDate,
    'yyyy-MM-dd',
    [System.Globalization.CultureInfo]::InvariantCulture)

if ([string]::IsNullOrWhiteSpace($RootPath))
{
    $RootPath = if ($PSScriptRoot)
    {
        (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    }
    else
    {
        throw 'RootPath was not provided and script root could not be determined.'
    }
}

$repoRoot = (Resolve-Path $RootPath).Path

function Resolve-RepositoryPath
{
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path))
    {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Get-HashFromManifest
{
    param(
        [Parameter(Mandatory)]
        [string]$ManifestPath,

        [Parameter(Mandatory)]
        [string]$AssetName
    )

    if (-not (Test-Path -LiteralPath $ManifestPath))
    {
        throw "SHA256 manifest not found at '$ManifestPath'."
    }

    foreach ($line in Get-Content -LiteralPath $ManifestPath)
    {
        if ($line -match '^(?<hash>[0-9a-fA-F]{64})\s+(?<name>.+)$' -and
            $Matches.name.Trim() -ieq $AssetName)
        {
            return $Matches.hash
        }
    }

    throw "SHA256 manifest '$ManifestPath' does not contain '$AssetName'."
}

$fullAssetName = "UniversalDeviceToolkit_v${Version}_Full_Setup.exe"
$onlineAssetName = "UniversalDeviceToolkit_v${Version}_Online_Setup.exe"
$fullZipAssetName = "UniversalDeviceToolkit_v${Version}_Full_win-x64.zip"

if (-not [string]::IsNullOrWhiteSpace($HashManifestPath))
{
    $resolvedHashManifestPath = Resolve-RepositoryPath $HashManifestPath
    $fullHash = Get-HashFromManifest -ManifestPath $resolvedHashManifestPath -AssetName $fullAssetName
    $onlineHash = Get-HashFromManifest -ManifestPath $resolvedHashManifestPath -AssetName $onlineAssetName
    $portableHash = Get-HashFromManifest -ManifestPath $resolvedHashManifestPath -AssetName $fullZipAssetName

    # Full and Online setup names are copies of one WebView2 installer.
    # Unsigned copies share a hash; signing each copy makes those hashes differ.
    if ($fullHash.ToUpperInvariant() -ceq $portableHash.ToUpperInvariant() -or $onlineHash.ToUpperInvariant() -ceq $portableHash.ToUpperInvariant())
    {
        throw "Setup and Full ZIP SHA256 hashes must be distinct."
    }

    if ([string]::IsNullOrWhiteSpace($InstallerSha256))
    {
        $InstallerSha256 = $fullHash
    }
    elseif ($InstallerSha256.ToUpperInvariant() -cne $fullHash.ToUpperInvariant())
    {
        throw "Installer SHA256 '$InstallerSha256' does not match '$fullAssetName' in '$HashManifestPath'."
    }

    if ([string]::IsNullOrWhiteSpace($PortableSha256)) { $PortableSha256 = $portableHash }
    elseif ($PortableSha256.ToUpperInvariant() -cne $portableHash.ToUpperInvariant()) {
        throw "Portable SHA256 '$PortableSha256' does not match '$fullZipAssetName' in '$HashManifestPath'."
    }
}

if ([string]::IsNullOrWhiteSpace($InstallerSha256))
{
    throw 'Provide either -InstallerSha256 or -HashManifestPath.'
}
if ([string]::IsNullOrWhiteSpace($PortableSha256))
{
    throw 'Provide either -PortableSha256 or -HashManifestPath.'
}

$installerSha256Upper = $InstallerSha256.ToUpperInvariant()
$portableSha256Lower = $PortableSha256.ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($InstallerUrl))
{
    $InstallerUrl = "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/download/v${Version}/$fullAssetName"
}

$portableUrl = "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/download/v${Version}/$fullZipAssetName"

function Ensure-Directory
{
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path))
    {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Write-Utf8NoBomFile
{
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    $parent = Split-Path -Parent $Path
    Ensure-Directory -Path $parent
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Get-ScoopManifestContent
{
    param(
        [Parameter(Mandatory)]
        [string]$Version,

        [Parameter(Mandatory)]
        [string]$PortableUrl,

        [Parameter(Mandatory)]
        [string]$PortableSha256,

        [Parameter(Mandatory)]
        [string]$Notes
    )

    return @"
{
  "version": "$Version",
  "description": "Universal Device Toolkit is a lightweight, open-source utility for supported Lenovo Legion, LOQ, Ideapad Gaming, and related laptops.",
  "homepage": "https://github.com/SSC-STUDIO/UniversalDeviceToolkit",
  "license": "GPL-3.0-only",
  "notes": "$Notes",
  "architecture": {
    "64bit": {
      "url": "$PortableUrl",
      "hash": "$PortableSha256"
    }
  },
  "innosetup": false,
  "shortcuts": [
    [
      "UniversalDeviceToolkit.exe",
      "Universal Device Toolkit"
    ]
  ],
  "checkver": {
    "url": "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest",
    "regex": "/releases/tag/v([\\d.]+)"
  },
  "autoupdate": {
    "architecture": {
      "64bit": {
         "url": "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/download/v`$version/UniversalDeviceToolkit_v`$version_Full_win-x64.zip"
      }
    }
  }
}
"@
}

$wingetVersionDirectory = Join-Path $repoRoot "Packaging\winget\manifests\s\SSC-STUDIO\UniversalDeviceToolkit\$Version"
Ensure-Directory -Path $wingetVersionDirectory

$wingetVersionManifestPath = Join-Path $wingetVersionDirectory 'SSC-STUDIO.UniversalDeviceToolkit.yaml'
$wingetLocaleManifestPath = Join-Path $wingetVersionDirectory 'SSC-STUDIO.UniversalDeviceToolkit.locale.en-US.yaml'
$wingetInstallerManifestPath = Join-Path $wingetVersionDirectory 'SSC-STUDIO.UniversalDeviceToolkit.installer.yaml'

$wingetVersionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json

PackageIdentifier: SSC-STUDIO.UniversalDeviceToolkit
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.10.0
"@

$wingetLocaleManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json

PackageIdentifier: SSC-STUDIO.UniversalDeviceToolkit
PackageVersion: $Version
PackageLocale: en-US
Publisher: SSC-STUDIO
PublisherUrl: https://github.com/SSC-STUDIO
PublisherSupportUrl: https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues
PackageName: Universal Device Toolkit
PackageUrl: https://github.com/SSC-STUDIO/UniversalDeviceToolkit
License: GPL-3.0
LicenseUrl: https://github.com/SSC-STUDIO/UniversalDeviceToolkit/blob/master/LICENSE
Copyright: Copyright (C) Universal Device Toolkit contributors
ShortDescription: Lightweight open-source Windows device toolkit - Legion hardware control, plugins, no telemetry.
Description: Universal Device Toolkit (UDT) is a lightweight, open-source Windows utility. Supported Lenovo Legion, LOQ, and IdeaPad Gaming laptops get direct hardware controls (power modes, RGB, dGPU, battery, Custom Mode, and more). Other Lenovo models and non-Lenovo PCs run in basic mode with plugins, system optimization, themes, and updates. No background service, no telemetry, no account.
Moniker: universaldevicetoolkit
Tags:
- lenovo
- legion
- loq
- vantage
- toolkit
- rgb
- laptop
- hardware-control
ReleaseNotesUrl: https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: 1.10.0
"@

$wingetInstallerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json

PackageIdentifier: SSC-STUDIO.UniversalDeviceToolkit
PackageVersion: $Version
InstallerType: exe
Scope: machine
UpgradeBehavior: install
ReleaseDate: $ReleaseDate
Installers:
- Architecture: x64
  InstallerUrl: $InstallerUrl
  InstallerSha256: $installerSha256Upper
  InstallerSwitches:
    Silent: /S
    SilentWithProgress: /S
ManifestType: installer
ManifestVersion: 1.10.0
"@

Write-Utf8NoBomFile -Path $wingetVersionManifestPath -Content $wingetVersionManifest
Write-Utf8NoBomFile -Path $wingetLocaleManifestPath -Content $wingetLocaleManifest
Write-Utf8NoBomFile -Path $wingetInstallerManifestPath -Content $wingetInstallerManifest

$draftScoopManifestPath = Join-Path $repoRoot "Packaging\scoop\universaldevicetoolkit.$Version.draft.json"
$publishedScoopManifestPath = Join-Path $repoRoot 'Packaging\scoop\universaldevicetoolkit.json'

$draftScoopManifest = Get-ScoopManifestContent `
    -Version $Version `
    -PortableUrl $portableUrl `
    -PortableSha256 $portableSha256Lower `
    -Notes "Draft manifest generated from release metadata for version $Version. Publish it to the Scoop bucket only after validating install behavior."

Write-Utf8NoBomFile -Path $draftScoopManifestPath -Content $draftScoopManifest

if ($UpdatePublishedScoopManifest)
{
    $publishedScoopManifest = Get-ScoopManifestContent `
        -Version $Version `
        -PortableUrl $portableUrl `
        -PortableSha256 $portableSha256Lower `
        -Notes 'Universal Device Toolkit 6.x uses the universaldevicetoolkit Scoop package identity.'

    Write-Utf8NoBomFile -Path $publishedScoopManifestPath -Content $publishedScoopManifest
}

Write-Host "Updated winget manifests in: $wingetVersionDirectory"
Write-Host "Updated scoop draft manifest: $draftScoopManifestPath"
Write-Host "Winget points at Full Setup; Scoop points at Full Electron ZIP."

if ($UpdatePublishedScoopManifest)
{
    Write-Host "Updated published scoop manifest: $publishedScoopManifestPath"
}
