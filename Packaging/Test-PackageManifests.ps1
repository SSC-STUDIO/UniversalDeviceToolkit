param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Repository = 'SSC-STUDIO/UniversalDeviceToolkit',

    [string]$HashManifestPath,

    [string]$ExpectedInstallerSha256,

    [string]$ExpectedPortableSha256,

    [string]$ExpectedPackageIdentifier = 'SSC-STUDIO.UniversalDeviceToolkit',

    [string]$ExpectedPackageName = 'Universal Device Toolkit',

    [string]$ExpectedPublisher = 'SSC-STUDIO',

    [string]$ElectronBuilderConfig = 'Apps\Electron\electron-builder.yml',

    [string]$WingetManifestDirectory,

    [string[]]$ScoopManifestPaths
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Resolve-RepositoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Read-YamlScalar {
    param(
        [AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$Key
    )

    $pattern = '^\s*' + [regex]::Escape($Key) + '\s*:\s*(.+?)\s*$'
    foreach ($line in $Lines) {
        if ($line -match $pattern) {
            return $Matches[1].Trim("'`"")
        }
    }

    throw "Could not find YAML key '$Key'."
}

function Get-HashFromManifest {
    param(
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][string]$AssetName
    )

    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "SHA256 manifest not found at '$ManifestPath'."
    }

    foreach ($line in Get-Content -LiteralPath $ManifestPath) {
        if ($line -match '^(?<hash>[0-9a-fA-F]{64})\s+(?<name>.+)$' -and
            $Matches.name.Trim() -ieq $AssetName) {
            return $Matches.hash.ToUpperInvariant()
        }
    }

    throw "SHA256 manifest '$ManifestPath' does not contain '$AssetName'."
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [AllowNull()][string]$Actual,
        [AllowNull()][string]$Expected
    )

    if ($Actual -cne $Expected) {
        throw "$Name mismatch. Expected '$Expected', got '$Actual'."
    }
}

function Get-ElectronBuilderValue {
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        throw "electron-builder config not found at '$ConfigPath'."
    }

    $pattern = '^\s*' + [regex]::Escape($Name) + '\s*:\s*(?:"(?<value>[^"]+)"|(?<value>[^#]+?))\s*(?:#.*)?$'
    foreach ($line in Get-Content -LiteralPath $ConfigPath) {
        if ($line -match $pattern) {
            return $Matches.value.Trim()
        }
    }

    throw "Could not find electron-builder key '$Name'."
}

$fullAssetName = "UniversalDeviceToolkit_v${Version}_Full_Setup.exe"
$onlineAssetName = "UniversalDeviceToolkit_v${Version}_Online_Setup.exe"
$fullZipAssetName = "UniversalDeviceToolkit_v${Version}_Full_win-x64.zip"
$expectedInstallerUrl = "https://github.com/$Repository/releases/download/v$Version/$fullAssetName"
$expectedPortableUrl = "https://github.com/$Repository/releases/download/v$Version/$fullZipAssetName"

if (-not [string]::IsNullOrWhiteSpace($HashManifestPath)) {
    $resolvedHashManifestPath = Resolve-RepositoryPath $HashManifestPath
    $fullHash = Get-HashFromManifest -ManifestPath $resolvedHashManifestPath -AssetName $fullAssetName
    $onlineHash = Get-HashFromManifest -ManifestPath $resolvedHashManifestPath -AssetName $onlineAssetName
    $portableHash = Get-HashFromManifest -ManifestPath $resolvedHashManifestPath -AssetName $fullZipAssetName

    # Full and Online setup names are copies of one WebView2 installer.
    if ($fullHash.ToUpperInvariant() -ceq $portableHash.ToUpperInvariant() -or $onlineHash.ToUpperInvariant() -ceq $portableHash.ToUpperInvariant()) {
        throw "Setup and Full ZIP SHA256 hashes must be distinct."
    }

    if ([string]::IsNullOrWhiteSpace($ExpectedInstallerSha256)) {
        $ExpectedInstallerSha256 = $fullHash
    }
    else {
        Assert-Equal 'Expected installer SHA256 and release SHA256 manifest' $ExpectedInstallerSha256.ToUpperInvariant() $fullHash.ToUpperInvariant()
    }
    if ([string]::IsNullOrWhiteSpace($ExpectedPortableSha256)) { $ExpectedPortableSha256 = $portableHash }
    else { Assert-Equal 'Expected portable SHA256 and release SHA256 manifest' $ExpectedPortableSha256.ToUpperInvariant() $portableHash.ToUpperInvariant() }
}

if ([string]::IsNullOrWhiteSpace($ExpectedInstallerSha256)) {
    throw 'Provide either -HashManifestPath or -ExpectedInstallerSha256.'
}
if ([string]::IsNullOrWhiteSpace($ExpectedPortableSha256)) { throw 'Provide either -HashManifestPath or -ExpectedPortableSha256.' }

$expectedInstallerSha256Upper = $ExpectedInstallerSha256.ToUpperInvariant()
$expectedInstallerSha256Lower = $ExpectedInstallerSha256.ToLowerInvariant()
$expectedPortableSha256Lower = $ExpectedPortableSha256.ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($WingetManifestDirectory)) {
    $WingetManifestDirectory = "Packaging\winget\manifests\s\SSC-STUDIO\UniversalDeviceToolkit\$Version"
}

$resolvedWingetDirectory = Resolve-RepositoryPath $WingetManifestDirectory
$wingetVersionManifestPath = Join-Path $resolvedWingetDirectory 'SSC-STUDIO.UniversalDeviceToolkit.yaml'
$wingetLocaleManifestPath = Join-Path $resolvedWingetDirectory 'SSC-STUDIO.UniversalDeviceToolkit.locale.en-US.yaml'
$wingetInstallerManifestPath = Join-Path $resolvedWingetDirectory 'SSC-STUDIO.UniversalDeviceToolkit.installer.yaml'
if (-not (Test-Path -LiteralPath $wingetVersionManifestPath)) {
    throw "winget version manifest not found at '$wingetVersionManifestPath'."
}
if (-not (Test-Path -LiteralPath $wingetLocaleManifestPath)) {
    throw "winget locale manifest not found at '$wingetLocaleManifestPath'."
}
if (-not (Test-Path -LiteralPath $wingetInstallerManifestPath)) {
    throw "winget installer manifest not found at '$wingetInstallerManifestPath'."
}

$resolvedInstallerConfigPath = Resolve-RepositoryPath $ElectronBuilderConfig
$installerPublisher = Get-ElectronBuilderValue -ConfigPath $resolvedInstallerConfigPath -Name 'productName'
$electronExecutableName = Get-ElectronBuilderValue -ConfigPath $resolvedInstallerConfigPath -Name 'executableName'
$expectedShortcutExecutable = "$electronExecutableName.exe"
$wingetVersionLines = Get-Content -LiteralPath $wingetVersionManifestPath
$wingetLocaleLines = Get-Content -LiteralPath $wingetLocaleManifestPath

Assert-Equal 'installer productName' $installerPublisher $ExpectedPackageName
Assert-Equal 'winget version PackageIdentifier' (Read-YamlScalar -Lines $wingetVersionLines -Key 'PackageIdentifier') $ExpectedPackageIdentifier
Assert-Equal 'winget version PackageVersion' (Read-YamlScalar -Lines $wingetVersionLines -Key 'PackageVersion') $Version
Assert-Equal 'winget version DefaultLocale' (Read-YamlScalar -Lines $wingetVersionLines -Key 'DefaultLocale') 'en-US'
Assert-Equal 'winget version ManifestType' (Read-YamlScalar -Lines $wingetVersionLines -Key 'ManifestType') 'version'
Assert-Equal 'winget locale PackageIdentifier' (Read-YamlScalar -Lines $wingetLocaleLines -Key 'PackageIdentifier') $ExpectedPackageIdentifier
Assert-Equal 'winget locale PackageVersion' (Read-YamlScalar -Lines $wingetLocaleLines -Key 'PackageVersion') $Version
Assert-Equal 'winget Publisher' (Read-YamlScalar -Lines $wingetLocaleLines -Key 'Publisher') $ExpectedPublisher
Assert-Equal 'winget PackageName' (Read-YamlScalar -Lines $wingetLocaleLines -Key 'PackageName') $ExpectedPackageName

$wingetLines = Get-Content -LiteralPath $wingetInstallerManifestPath
Assert-Equal 'winget installer PackageIdentifier' (Read-YamlScalar -Lines $wingetLines -Key 'PackageIdentifier') $ExpectedPackageIdentifier
Assert-Equal 'winget PackageVersion' (Read-YamlScalar -Lines $wingetLines -Key 'PackageVersion') $Version
Assert-Equal 'winget InstallerType' (Read-YamlScalar -Lines $wingetLines -Key 'InstallerType') 'exe'
Assert-Equal 'winget Scope' (Read-YamlScalar -Lines $wingetLines -Key 'Scope') 'machine'
Assert-Equal 'winget InstallerUrl' (Read-YamlScalar -Lines $wingetLines -Key 'InstallerUrl') $expectedInstallerUrl
Assert-Equal 'winget InstallerSha256' (Read-YamlScalar -Lines $wingetLines -Key 'InstallerSha256').ToUpperInvariant() $expectedInstallerSha256Upper
Assert-Equal 'winget Silent switch' (Read-YamlScalar -Lines $wingetLines -Key 'Silent') '/S'
Assert-Equal 'winget SilentWithProgress switch' (Read-YamlScalar -Lines $wingetLines -Key 'SilentWithProgress') '/S'

$prepareScriptPath = Join-Path $PSScriptRoot 'Prepare-PackageManifests.ps1'
if (-not (Test-Path -LiteralPath $prepareScriptPath)) {
    throw "Package manifest generator not found at '$prepareScriptPath'."
}

$prepareScript = Get-Content -Raw -LiteralPath $prepareScriptPath
if ($prepareScript -notlike '*Silent: /S*' -or $prepareScript -notlike '*SilentWithProgress: /S*') {
    throw "Winget generator must emit electron-builder NSIS Silent switch /S."
}
if ($prepareScript -like '*--silent*' -or $prepareScript -like '*/SILENT*' -or $prepareScript -like '*/VERYSILENT*') {
    throw "Winget generator must not emit --silent, /SILENT, or /VERYSILENT."
}
if ($prepareScript -notlike '*"innosetup": false*') {
    throw "Scoop generator Get-ScoopManifestContent must emit innosetup: false."
}
if ($prepareScript -notlike '*"UniversalDeviceToolkit.exe"*') {
    throw "Scoop generator Get-ScoopManifestContent must emit shortcut UniversalDeviceToolkit.exe."
}
if ($prepareScript -like '*"Universal Device Toolkit.exe"*') {
    throw "Scoop generator Get-ScoopManifestContent must not emit WPF shortcut Universal Device Toolkit.exe."
}

if ($ScoopManifestPaths.Count -eq 0) {
    $defaultScoopPaths = @(
        'Packaging\scoop\universaldevicetoolkit.json',
        "Packaging\scoop\universaldevicetoolkit.$Version.draft.json"
    )

    $ScoopManifestPaths = @(
        $defaultScoopPaths |
            Where-Object { Test-Path -LiteralPath (Resolve-RepositoryPath $_) }
    )
}

if ($ScoopManifestPaths.Count -eq 0) {
    throw 'No Scoop manifests were provided or found for validation.'
}

foreach ($scoopManifestPath in $ScoopManifestPaths) {
    $resolvedScoopManifestPath = Resolve-RepositoryPath $scoopManifestPath
    if (-not (Test-Path -LiteralPath $resolvedScoopManifestPath)) {
        throw "Scoop manifest not found at '$resolvedScoopManifestPath'."
    }

    $scoopManifest = Get-Content -Raw -LiteralPath $resolvedScoopManifestPath | ConvertFrom-Json
    Assert-Equal "Scoop version in '$scoopManifestPath'" ([string]$scoopManifest.version) $Version
    Assert-Equal "Scoop URL in '$scoopManifestPath'" ([string]$scoopManifest.architecture.'64bit'.url) $expectedPortableUrl
    Assert-Equal "Scoop hash in '$scoopManifestPath'" ([string]$scoopManifest.architecture.'64bit'.hash) $expectedPortableSha256Lower

    $scoopFileName = [System.IO.Path]::GetFileName($resolvedScoopManifestPath)
    $isHistoricalWpfScoopManifest = $scoopFileName -like 'lenovolegiontoolkit*'
    $isDraftScoopManifest = $scoopFileName -like '*.draft.json'
    if ($isHistoricalWpfScoopManifest -or -not $isDraftScoopManifest) {
        continue
    }

    Assert-Equal "Scoop innosetup in '$scoopManifestPath'" ([string]$scoopManifest.innosetup) 'False'
    if ([string]$scoopManifest.shortcuts[0][0] -cne $expectedShortcutExecutable) {
        throw "Scoop shortcut in '$scoopManifestPath' must target $expectedShortcutExecutable from electron-builder executableName."
    }
}

Write-Host "Package manifests match $fullAssetName, $fullZipAssetName, and SHA256 $expectedInstallerSha256Upper / $expectedPortableSha256Lower."
