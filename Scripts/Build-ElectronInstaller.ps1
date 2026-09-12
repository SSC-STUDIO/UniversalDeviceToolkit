[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [string]$InstallerOutput = 'BuildInstaller',

    [string]$PayloadOutput = 'BuildInstallerPayload',

    [switch]$PreparePayloadsOnly,

    [switch]$PrepareInstallerShellOnly,

    [switch]$PackagePreparedPayloads
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Builds the branded Electron installer (UniversalDeviceToolkit.Electron) in
# both flavors, mirroring the retired WPF installer output layout:
#   BuildInstaller\UniversalDeviceToolkitSetup.exe
#   BuildInstaller\UniversalDeviceToolkitOnlineSetup.exe
# The release workflow runs this script in three phases. It stages the Full and
# Online application payloads, then the custom installer shell, and finally
# wraps those signed trees without rebuilding them. Calling the script without
# a phase switch keeps the convenient local behavior by running every phase.

$selectedPhaseCount = @(
    [bool]$PreparePayloadsOnly
    [bool]$PrepareInstallerShellOnly
    [bool]$PackagePreparedPayloads
).Where({ $_ }).Count
if ($selectedPhaseCount -gt 1) {
    throw 'Only one installer build phase switch can be used at a time.'
}

$runPreparePayloads = $selectedPhaseCount -eq 0 -or $PreparePayloadsOnly
$runPrepareInstallerShell = $selectedPhaseCount -eq 0 -or $PrepareInstallerShellOnly
$runPackagePreparedPayloads = $selectedPhaseCount -eq 0 -or $PackagePreparedPayloads

$repoRoot = Split-Path -Parent $PSScriptRoot
$electronProject = Join-Path $repoRoot 'Apps/Electron'
$hostPublishDir = Join-Path $repoRoot 'Apps\Host\publish\win-x64'
$pruneScript = Join-Path $PSScriptRoot 'Prune-ShippingFootprint.ps1'
$channelFile = Join-Path $electronProject 'resources\install-channel'
$distDir = Join-Path $electronProject 'dist'
$unpackedDir = Join-Path $distDir 'win-unpacked'
$customDistDir = Join-Path $distDir 'custom-installer'
$customOnlineDistDir = Join-Path $distDir 'custom-online-installer'
$payloadOutputPath = if ([System.IO.Path]::IsPathRooted($PayloadOutput)) {
    $PayloadOutput
}
else {
    Join-Path $repoRoot $PayloadOutput
}
$fullPayloadDir = Join-Path $payloadOutputPath 'full'
$onlinePayloadDir = Join-Path $payloadOutputPath 'online'
$installerShellDir = Join-Path $payloadOutputPath 'installer-shell'
$nsisToolsetDir = Join-Path $payloadOutputPath 'nsis'

if (-not (Test-Path -LiteralPath (Join-Path $electronProject 'package.json'))) {
    throw "Electron project not found at '$electronProject'."
}

$installerOutputPath = Join-Path $repoRoot $InstallerOutput
New-Item -ItemType Directory -Path $installerOutputPath -Force | Out-Null

function Set-InstallChannel {
    param([Parameter(Mandatory = $true)][string]$Channel)

    $resourcesDir = Split-Path -Parent $channelFile
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null
    Set-Content -LiteralPath $channelFile -Value $Channel -Encoding ascii -NoNewline
}

function Set-ElectronPackageVersion {
    # electron-builder reads the version from package.json. Keep the full
    # SemVer (including preview labels) so a v6.0.0-preview.1 release does
    # not stamp the Electron app as stable 6.0.0.
    $packageJsonPath = Join-Path $electronProject 'package.json'
    $packageJsonText = [System.IO.File]::ReadAllText($packageJsonPath)
    $versionMatch = [System.Text.RegularExpressions.Regex]::Match(
        $packageJsonText,
        '(?m)^(\s*"version"\s*:\s*)"[^"]*"')
    if (-not $versionMatch.Success) {
        throw "Electron package.json does not contain a top-level version field."
    }

    $versionReplacement = $versionMatch.Groups[1].Value + '"' + $Version + '"'
    $updatedPackageJson = $packageJsonText.Remove($versionMatch.Index, $versionMatch.Length).
        Insert($versionMatch.Index, $versionReplacement)
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($packageJsonPath, $updatedPackageJson, $utf8WithoutBom)
}

function Invoke-ElectronWinTarget {
    param(
        [Parameter(Mandatory = $true)][string]$Target,
        [string]$PrepackagedPath
    )

    $arguments = @('electron-builder', '--config', 'electron-builder.yml', '--win', $Target, '--publish', 'never')
    if (-not [string]::IsNullOrWhiteSpace($PrepackagedPath)) {
        $arguments += @('--prepackaged', $PrepackagedPath)
    }

    & npx @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "electron-builder ($Target) failed."
    }
}

function Move-UnpackedPayload {
    param([Parameter(Mandatory = $true)][string]$Destination)

    if (-not (Test-Path -LiteralPath $unpackedDir -PathType Container)) {
        throw "Electron unpacked payload not found at '$unpackedDir'."
    }
    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force | Out-Null
    Move-Item -LiteralPath $unpackedDir -Destination $Destination
}

function Assert-PreparedPayload {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $electronExecutable = Join-Path $Path 'UniversalDeviceToolkit.exe'
    $hostDirectory = Join-Path $Path 'resources\host'
    if (-not (Test-Path -LiteralPath $electronExecutable -PathType Leaf)) {
        throw "$Description payload is missing '$electronExecutable'."
    }
    if (-not (Test-Path -LiteralPath $hostDirectory -PathType Container)) {
        throw "$Description payload is missing '$hostDirectory'."
    }
}

function Assert-PreparedInstallerShell {
    $installerExecutable = Join-Path $installerShellDir 'Universal Device Toolkit Setup.exe'
    $embeddedPayload = Join-Path $installerShellDir 'resources\payload\UniversalDeviceToolkit.exe'
    $elevateExecutable = Join-Path $nsisToolsetDir 'elevate.exe'
    if (-not (Test-Path -LiteralPath $installerExecutable -PathType Leaf)) {
        throw "Prepared installer shell is missing '$installerExecutable'."
    }
    if (-not (Test-Path -LiteralPath $embeddedPayload -PathType Leaf)) {
        throw "Prepared installer shell is missing '$embeddedPayload'."
    }
    if (-not (Test-Path -LiteralPath $elevateExecutable -PathType Leaf)) {
        throw "Prepared NSIS toolset is missing '$elevateExecutable'."
    }
}

function Get-LatestArtifact {
    param(
        [Parameter(Mandatory = $true)][string]$Filter,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $artifact = Get-ChildItem -LiteralPath $distDir -Filter $Filter -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $artifact) {
        throw "$Description artifact not found under '$distDir'."
    }
    return $artifact
}

function Assert-ElectronZip {
    param([Parameter(Mandatory = $true)][string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.TrimStart('/') })
        if (-not ($entries | Where-Object { $_ -match '(^|/)UniversalDeviceToolkit\.exe$' })) {
            throw "Electron ZIP '$Path' does not contain UniversalDeviceToolkit.exe."
        }
        if (-not ($entries | Where-Object { $_ -match '(^|/)resources/host/|(^|/)resources\\host\\' })) {
            throw "Electron ZIP '$Path' does not contain resources/host."
        }
    }
    finally {
        $archive.Dispose()
    }
}

# Build the renderer + main process and stage the exact payload trees that will
# be signed before packaging.
Push-Location $electronProject
try {
    Set-ElectronPackageVersion

    if ($runPreparePayloads) {
        if (Test-Path -LiteralPath $distDir) {
            Remove-Item -LiteralPath $distDir -Recurse -Force
        }
        if (Test-Path -LiteralPath $payloadOutputPath) {
            Remove-Item -LiteralPath $payloadOutputPath -Recurse -Force
        }

        npm run build
        if ($LASTEXITCODE -ne 0) {
            throw 'Electron build failed.'
        }

        Set-InstallChannel -Channel 'full'
        Invoke-ElectronWinTarget -Target 'dir'
        Move-UnpackedPayload -Destination $fullPayloadDir

        if (Test-Path -LiteralPath $hostPublishDir) {
            & $pruneScript -PayloadPath $hostPublishDir -AllowedCultures 'en'
        }
        else {
            Write-Warning "Host publish directory not found at '$hostPublishDir'; Online payload will not be English-pruned."
        }

        Set-InstallChannel -Channel 'online'
        Invoke-ElectronWinTarget -Target 'dir'
        Move-UnpackedPayload -Destination $onlinePayloadDir

        Assert-PreparedPayload -Path $fullPayloadDir -Description 'Full'
        Assert-PreparedPayload -Path $onlinePayloadDir -Description 'Online'
        Write-Host "Electron payloads prepared for signing under '$payloadOutputPath'."
    }

    if ($runPrepareInstallerShell) {
        Assert-PreparedPayload -Path $fullPayloadDir -Description 'Full'
        Assert-PreparedPayload -Path $onlinePayloadDir -Description 'Online'

        if (Test-Path -LiteralPath $distDir) {
            Remove-Item -LiteralPath $distDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $distDir -Force | Out-Null

        # custom-installer.yml embeds dist/win-unpacked. Stage its unpacked
        # Electron shell separately so CI can sign the executable that becomes
        # the installed uninstaller before the portable wrapper is created.
        Copy-Item -LiteralPath $fullPayloadDir -Destination $unpackedDir -Recurse -Force
        & npx electron-builder --config custom-installer.yml --win dir --publish never
        if ($LASTEXITCODE -ne 0) {
            throw 'Custom Electron installer shell preparation failed.'
        }

        $customUnpackedDir = Join-Path $customDistDir 'win-unpacked'
        if (Test-Path -LiteralPath $installerShellDir) {
            Remove-Item -LiteralPath $installerShellDir -Recurse -Force
        }
        Move-Item -LiteralPath $customUnpackedDir -Destination $installerShellDir

        & node scripts/stage-nsis-toolset.mjs $nsisToolsetDir
        if ($LASTEXITCODE -ne 0) {
            throw 'NSIS toolset staging failed.'
        }
        Assert-PreparedInstallerShell
        Write-Host "Custom installer shell prepared for signing at '$installerShellDir'."
    }

    if ($runPackagePreparedPayloads) {
        Assert-PreparedPayload -Path $fullPayloadDir -Description 'Full'
        Assert-PreparedPayload -Path $onlinePayloadDir -Description 'Online'
        Assert-PreparedInstallerShell

        if (Test-Path -LiteralPath $distDir) {
            Remove-Item -LiteralPath $distDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $distDir -Force | Out-Null
        Copy-Item -LiteralPath $fullPayloadDir -Destination $unpackedDir -Recurse -Force

        $previousNsisToolset = [System.Environment]::GetEnvironmentVariable('ELECTRON_BUILDER_NSIS_DIR')
        try {
            $env:ELECTRON_BUILDER_NSIS_DIR = $nsisToolsetDir

            & npx electron-builder --config custom-installer.yml --win portable --prepackaged $installerShellDir --publish never
            if ($LASTEXITCODE -ne 0) {
                throw 'Custom Electron Full installer failed.'
            }

            & npx electron-builder --config custom-online-installer.yml --win portable --publish never
            if ($LASTEXITCODE -ne 0) {
                throw 'Custom Electron Online installer failed.'
            }

            Invoke-ElectronWinTarget -Target 'zip' -PrepackagedPath $fullPayloadDir

            $fullZipArtifact = Get-LatestArtifact -Filter 'UniversalDeviceToolkitSetup-*.zip' -Description 'Electron Full ZIP'
            $fullZipPath = Join-Path $installerOutputPath "UniversalDeviceToolkit_v${Version}_Full_win-x64.zip"
            Copy-Item -LiteralPath $fullZipArtifact.FullName -Destination $fullZipPath -Force
            Assert-ElectronZip -Path $fullZipPath
            Write-Host "Electron Full ZIP built: $fullZipPath"
            Remove-Item -LiteralPath $fullZipArtifact.FullName -Force

            Invoke-ElectronWinTarget -Target 'zip' -PrepackagedPath $onlinePayloadDir
        }
        finally {
            if ($null -eq $previousNsisToolset) {
                Remove-Item Env:ELECTRON_BUILDER_NSIS_DIR -ErrorAction SilentlyContinue
            }
            else {
                $env:ELECTRON_BUILDER_NSIS_DIR = $previousNsisToolset
            }
        }
    }
}
finally {
    Pop-Location
}

if (-not $runPackagePreparedPayloads) {
    return
}

# Locate the produced custom full setup artifact.
# Full (offline) uses the portable custom setup app.
# Online uses the portable custom online setup app.
$customSetupArtifact = Get-ChildItem -LiteralPath $customDistDir -Filter 'UniversalDeviceToolkitSetup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $customSetupArtifact) {
    throw "Custom Full setup artifact not found under '$customDistDir'."
}

$finalSetupPath = Join-Path $installerOutputPath 'UniversalDeviceToolkitSetup.exe'
Copy-Item -LiteralPath $customSetupArtifact.FullName -Destination $finalSetupPath -Force
Write-Host "Custom Electron Full installer built: $finalSetupPath ($([math]::Round($customSetupArtifact.Length / 1MB, 1)) MB)"

$onlineArtifact = Get-ChildItem -LiteralPath $customOnlineDistDir -Filter 'UniversalDeviceToolkitOnlineSetup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $onlineArtifact) {
    throw "Custom Online setup artifact not found under '$customOnlineDistDir'."
}

$onlineZipArtifact = Get-LatestArtifact -Filter 'UniversalDeviceToolkitSetup-*.zip' -Description 'Electron Online ZIP'

$maxOnlineBytes = 85MB
if ($onlineArtifact.Length -gt $maxOnlineBytes) {
    throw "Online installer is $([math]::Round($onlineArtifact.Length / 1MB, 2)) MB; must be <= 85 MB."
}

$finalOnlinePath = Join-Path $installerOutputPath 'UniversalDeviceToolkitOnlineSetup.exe'
Copy-Item -LiteralPath $onlineArtifact.FullName -Destination $finalOnlinePath -Force
Write-Host "Custom Electron Online installer built: $finalOnlinePath ($([math]::Round($onlineArtifact.Length / 1MB, 2)) MB)"

$onlineZipPath = Join-Path $installerOutputPath "UniversalDeviceToolkit_v${Version}_Online_win-x64.zip"
Copy-Item -LiteralPath $onlineZipArtifact.FullName -Destination $onlineZipPath -Force
Assert-ElectronZip -Path $onlineZipPath
Write-Host "Electron Online ZIP built: $onlineZipPath"

$footprintAuditor = Join-Path $electronProject 'scripts\package-footprint.mjs'
& node $footprintAuditor $finalSetupPath $finalOnlinePath $fullZipPath $onlineZipPath
if ($LASTEXITCODE -ne 0) {
    throw 'Electron installer artifact footprint audit failed.'
}

$nsisPackages = @(Get-ChildItem -LiteralPath $distDir -Filter '*.nsis.7z' -Recurse -ErrorAction SilentlyContinue)
foreach ($package in $nsisPackages) {
    Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $installerOutputPath $package.Name) -Force
    Write-Host "Copied nsis-web package: $($package.Name) ($([math]::Round($package.Length / 1MB, 1)) MB)"
}

Get-ChildItem -LiteralPath $installerOutputPath -File |
    Select-Object Name, Length, LastWriteTime |
    Format-Table -AutoSize
