[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$AssetVersion,

    [string]$ProjectPath = 'Apps/CrossPlatformCLI/UniversalDeviceToolkit.CrossPlatform.csproj',

    [string]$PublishOutput = 'Build-CrossPlatformCli',

    [Parameter(Mandatory = $true)]
    [string]$ReleaseOutput,

    [string]$AssetPrefix = 'UniversalDeviceToolkit',

    [string]$HashFileName,

    [switch]$SkipHashUpdate
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -eq 'Core' -and $IsMacOS) {
    throw 'macOS support is temporarily paused. Source code is retained for future restoration; use Windows or Linux.'
}

function Get-MajorVersion {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($Value -notmatch '^(?<major>\d+)\.') {
        throw "$Name '$Value' must start with a semantic major version."
    }

    return [int]$Matches.major
}

function Assert-CrossPlatformCliReleaseAllowed {
    param(
        [Parameter(Mandatory = $true)][string]$BuildVersion,
        [Parameter(Mandatory = $true)][string]$PublishedVersion
    )

    $buildMajor = Get-MajorVersion -Value $BuildVersion -Name 'Version'
    $publishedMajor = Get-MajorVersion -Value $PublishedVersion -Name 'AssetVersion'

    if ($buildMajor -lt 5 -or $publishedMajor -lt 5) {
        throw "Cross-platform CLI assets are not published before 5.x.x. Version '$BuildVersion' and asset version '$PublishedVersion' were requested."
    }
}

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

function Get-Sha256Hash {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Cannot hash missing file '$Path'."
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).ProviderPath
    $stream = [System.IO.File]::OpenRead($resolvedPath)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $sha256.ComputeHash($stream)
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    return -join ($bytes | ForEach-Object { $_.ToString('x2') })
}

function Compress-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $items = @(Get-ChildItem -LiteralPath $SourceDir -Force)
    if ($items.Count -eq 0) {
        throw "Cannot create '$DestinationPath' because '$SourceDir' is empty."
    }

    Compress-Archive -Path (Join-Path $SourceDir '*') -DestinationPath $DestinationPath -CompressionLevel Optimal
}

function Add-HashLine {
    param(
        [Parameter(Mandatory = $true)][string]$HashPath,
        [Parameter(Mandatory = $true)][string]$AssetPath,
        [Parameter(Mandatory = $true)][string]$AssetName
    )

    $hashLine = "{0}  {1}" -f (Get-Sha256Hash -Path $AssetPath), $AssetName

    if (Test-Path -LiteralPath $HashPath) {
        $existingLines = [System.IO.File]::ReadAllLines($HashPath)
        $filteredLines = @($existingLines | Where-Object { $_ -notmatch "\s+$([regex]::Escape($AssetName))$" })
        $lines = @($filteredLines + $hashLine)
    }
    else {
        $lines = @($hashLine)
    }

    Set-Content -LiteralPath $HashPath -Value $lines -Encoding ASCII
}

function Write-CrossPlatformLaunchers {
    param([Parameter(Mandatory = $true)][string]$OutputPath)

    $unixLauncherPath = Join-Path $OutputPath 'udt'
    $windowsLauncherPath = Join-Path $OutputPath 'udt.cmd'
    $readmePath = Join-Path $OutputPath 'README.txt'

    $unixLauncher = @'
#!/usr/bin/env sh
case "$(uname -s)" in
  Darwin*)
    echo "macOS support is temporarily paused. Source code is retained for future restoration; use Windows or Linux." >&2
    exit 1
    ;;
esac
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec dotnet "$SCRIPT_DIR/udt.dll" "$@"
'@

    $windowsLauncher = @'
@echo off
dotnet "%~dp0udt.dll" %*
'@

    $readme = @'
Universal Device Toolkit cross-platform diagnostics CLI

Requires the .NET 10 runtime.

Windows:
  udt.cmd status

Linux:
  sh ./udt status
  chmod +x ./udt
  ./udt status

Windows/Linux:
  dotnet udt.dll status

macOS support is temporarily paused. Its source code is retained for future restoration.
'@

    Set-Content -LiteralPath $unixLauncherPath -Value $unixLauncher -Encoding ASCII
    Set-Content -LiteralPath $windowsLauncherPath -Value $windowsLauncher -Encoding ASCII
    Set-Content -LiteralPath $readmePath -Value $readme -Encoding ASCII
}

$project = Resolve-RepoPath $ProjectPath
$publishOutputPath = Resolve-RepoPath $PublishOutput
$releaseOutputPath = Resolve-RepoPath $ReleaseOutput
$resolvedAssetVersion = if ([string]::IsNullOrWhiteSpace($AssetVersion)) { $Version } else { $AssetVersion }
Assert-CrossPlatformCliReleaseAllowed -BuildVersion $Version -PublishedVersion $resolvedAssetVersion
$assetName = "${AssetPrefix}_v${resolvedAssetVersion}_CLI_cross-platform.zip"
$assetPath = Join-Path $releaseOutputPath $assetName
$resolvedHashFileName = if ([string]::IsNullOrWhiteSpace($HashFileName)) { "${AssetPrefix}_v${resolvedAssetVersion}_SHA256.txt" } else { $HashFileName }
$hashPath = Join-Path $releaseOutputPath $resolvedHashFileName

if (-not (Test-Path -LiteralPath $project)) {
    throw "Cross-platform CLI project not found at '$project'."
}

Remove-Item -LiteralPath $publishOutputPath -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishOutputPath, $releaseOutputPath -Force | Out-Null

dotnet publish $project --configuration Release --output $publishOutputPath /p:DebugType=None /p:FileVersion=$Version /p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    throw 'Cross-platform CLI publish failed.'
}

Write-CrossPlatformLaunchers -OutputPath $publishOutputPath

$requiredFiles = @('udt.dll', 'udt.deps.json', 'udt.runtimeconfig.json', 'udt', 'udt.cmd', 'README.txt')
foreach ($fileName in $requiredFiles) {
    $filePath = Join-Path $publishOutputPath $fileName
    if (-not (Test-Path -LiteralPath $filePath)) {
        throw "Published cross-platform CLI is missing '$fileName'."
    }
}

$shippingPayloadGuard = Resolve-RepoPath 'Scripts\Assert-ShippingPayload.ps1'
& $shippingPayloadGuard -PayloadPath $publishOutputPath

Compress-DirectoryContents -SourceDir $publishOutputPath -DestinationPath $assetPath
if (-not $SkipHashUpdate) {
    Add-HashLine -HashPath $hashPath -AssetPath $assetPath -AssetName $assetName
}

Write-Host "Prepared cross-platform CLI asset '$assetPath'."
if ($SkipHashUpdate) {
    Write-Host 'Skipped SHA256 manifest update.'
}
else {
    Write-Host "Updated SHA256 manifest '$hashPath'."
}
