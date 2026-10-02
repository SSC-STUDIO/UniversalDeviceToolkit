[CmdletBinding()]
param(
    [string]$BuildDir,

    [Alias('EnglishBuildDir')]
    [string]$OnlineBuildDir,

    # Directory that contains the published .NET Host payload
    # (UniversalDeviceToolkit.Host/publish/<rid>). Language packs are built from
    # the Host culture satellites. Defaults to BuildDir when not provided.
    [string]$HostBuildDir,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseOutput,

    [Parameter(Mandatory = $true)]
    [string]$PagesOutput,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$FinalizeOnly,

    [string]$FullInstallerPath,

    [string]$OnlineInstallerPath,

    [string]$FullZipPath,

    [string]$OnlineZipPath,

    [string]$PublicAssetPrefix = 'UniversalDeviceToolkit',

    [string]$ProductName = 'Universal Device Toolkit',

    [string]$Repository = 'SSC-STUDIO/UniversalDeviceToolkit',

    [string]$ResourcesBaseUrl = 'https://ssc-studio.github.io/UniversalDeviceToolkit/resources',

    [switch]$IncludeCrossPlatformCli
)

$ErrorActionPreference = 'Stop'

function Get-SharedSupportedCultures {
    $catalogPath = Join-Path $PSScriptRoot '..\UniversalDeviceToolkit.Lib.Abstractions\Localization\LocalizationCatalog.cs'
    if (-not (Test-Path -LiteralPath $catalogPath)) {
        throw "Shared localization catalog not found: $catalogPath"
    }

    $catalogText = Get-Content -LiteralPath $catalogPath -Raw
    $catalogBlock = [regex]::Match(
        $catalogText,
        'SupportedCultures\s*\{\s*get;\s*\}\s*=\s*\[(?<values>[\s\S]*?)\];')
    $cultures = @([regex]::Matches($catalogBlock.Groups['values'].Value, 'new\("([^"]+)"\)') |
        ForEach-Object { $_.Groups[1].Value })
    if ($cultures.Count -eq 0) {
        throw "Could not read supported cultures from $catalogPath"
    }

    return $cultures
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
    param([Parameter(Mandatory = $true)][string]$ReleaseVersion)

    if ((Get-MajorVersion -Value $ReleaseVersion -Name 'Version') -lt 5) {
        throw "Cross-platform CLI assets are not published before 5.x.x. Version '$ReleaseVersion' was requested."
    }
}

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PWD $Path))
}

function ConvertTo-UrlPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return $Path.Replace('\', '/')
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

function Get-FullSetupAssetName { param([string]$AssetVersion) "${PublicAssetPrefix}_v${AssetVersion}_Full_Setup.exe" }
function Get-OnlineSetupAssetName { param([string]$AssetVersion) "${PublicAssetPrefix}_v${AssetVersion}_Online_Setup.exe" }
function Get-FullZipAssetName { param([string]$AssetVersion) "${PublicAssetPrefix}_v${AssetVersion}_Full_win-x64.zip" }
function Get-OnlineZipAssetName { param([string]$AssetVersion) "${PublicAssetPrefix}_v${AssetVersion}_Online_win-x64.zip" }
function Get-CrossPlatformCliAssetName { param([string]$AssetVersion) "${PublicAssetPrefix}_v${AssetVersion}_CLI_cross-platform.zip" }
function Get-HashAssetName { param([string]$AssetVersion) "${PublicAssetPrefix}_v${AssetVersion}_SHA256.txt" }
function Get-LanguageAssetName { param([string]$AssetVersion, [string]$Culture) "$Culture.zip" }
function Get-DeviceAssetName { param([string]$PackId) "$PackId.zip" }

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

function Get-LanguageDirectories {
    # Localized satellite culture folders that the Online (English-only) copy
    # must not ship, regardless of which assembly produced the satellite.
    param([Parameter(Mandatory = $true)][string]$BuildPath)

    Get-ChildItem -LiteralPath $BuildPath -Directory |
        Where-Object {
            $_.Name -ne 'en' -and (Test-SatelliteDirectory $_.FullName)
        } |
        Sort-Object Name
}

function Test-SatelliteDirectory {
    param([Parameter(Mandatory = $true)][string]$DirectoryPath)

    $null -ne (
        Get-ChildItem -LiteralPath $DirectoryPath -File -Filter '*.resources.dll' -ErrorAction SilentlyContinue |
            Select-Object -First 1
    )
}

function Test-HostSatelliteDirectory {
    # The shipping app is the Electron shell plus UniversalDeviceToolkit.Host;
    # localized Host strings live in the UniversalDeviceToolkit.* satellite
    # assemblies (Lib, Lib.Automation, Lib.Macro). The retired WPF satellite
    # "Universal Device Toolkit.resources.dll" no longer exists.
    param([Parameter(Mandatory = $true)][string]$DirectoryPath)

    $null -ne (
        Get-ChildItem -LiteralPath $DirectoryPath -File -Filter 'UniversalDeviceToolkit.*.resources.dll' -ErrorAction SilentlyContinue |
            Select-Object -First 1
    )
}

function New-FileMetadata {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$CatalogPath,
        [string]$Url
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        throw "Expected asset not found at '$FilePath'."
    }

    $file = Get-Item -LiteralPath $FilePath
    $metadata = [ordered]@{
        name = $Name
        path = ConvertTo-UrlPath $CatalogPath
        size = $file.Length
        sha256 = Get-Sha256Hash -Path $file.FullName
    }

    if (-not [string]::IsNullOrWhiteSpace($Url)) {
        $metadata['url'] = $Url
    }

    return $metadata
}

function Get-LanguageDisplayName {
    param([Parameter(Mandatory = $true)][string]$Culture)

    try {
        return ([System.Globalization.CultureInfo]::GetCultureInfo($Culture)).EnglishName
    }
    catch {
        return $Culture
    }
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-HashFile {
    param(
        [Parameter(Mandatory = $true)][string[]]$AssetNames,
        [Parameter(Mandatory = $true)][string]$ReleaseOutputPath,
        [Parameter(Mandatory = $true)][string]$HashFileName
    )

    $lines = foreach ($assetName in $AssetNames) {
        $assetPath = Join-Path $ReleaseOutputPath $assetName
        if (-not (Test-Path -LiteralPath $assetPath)) {
            throw "Cannot write '$HashFileName' because '$assetName' is missing."
        }

        "{0}  {1}" -f (Get-Sha256Hash -Path $assetPath), $assetName
    }

    Set-Content -LiteralPath (Join-Path $ReleaseOutputPath $HashFileName) -Value $lines -Encoding ASCII
}

function Get-LanguagePackDefinitions {
    # Culture names use the BCP 47 canonical form from LocalizationCatalog.
    # Packaging fails when a supported culture produced no Host satellite
    # directory instead of publishing an incomplete pack.
    $definitions = @(
        @{ Culture = 'ar'; Directories = @('ar') },
        @{ Culture = 'bg'; Directories = @('bg') },
        @{ Culture = 'cs'; Directories = @('cs') },
        @{ Culture = 'de'; Directories = @('de') },
        @{ Culture = 'el'; Directories = @('el') },
        @{ Culture = 'es'; Directories = @('es') },
        @{ Culture = 'fr'; Directories = @('fr') },
        @{ Culture = 'hu'; Directories = @('hu') },
        @{ Culture = 'it'; Directories = @('it') },
        @{ Culture = 'ja'; Directories = @('ja') },
        @{ Culture = 'lv'; Directories = @('lv') },
        @{ Culture = 'nl-NL'; Directories = @('nl-NL') },
        @{ Culture = 'pl'; Directories = @('pl') },
        @{ Culture = 'pt'; Directories = @('pt') },
        @{ Culture = 'pt-BR'; Directories = @('pt-BR') },
        @{ Culture = 'ro'; Directories = @('ro') },
        @{ Culture = 'ru'; Directories = @('ru') },
        @{ Culture = 'sk'; Directories = @('sk') },
        @{ Culture = 'tr'; Directories = @('tr') },
        @{ Culture = 'uk'; Directories = @('uk') },
        @{ Culture = 'vi'; Directories = @('vi') },
        @{ Culture = 'zh-Hans'; Directories = @('zh-Hans') },
        @{ Culture = 'zh-Hant'; Directories = @('zh-Hant') },
        @{ Culture = 'uz-Latn-UZ'; Directories = @('uz-Latn-UZ') }
    )

    $expectedCultures = @(Get-SharedSupportedCultures | Where-Object { $_ -ne 'en' })
    $actualCultures = @($definitions | ForEach-Object { [string]$_.Culture })
    if (($expectedCultures -join '|') -ne ($actualCultures -join '|')) {
        throw "Language pack definitions do not match LocalizationCatalog. Expected '$($expectedCultures -join ', ')', got '$($actualCultures -join ', ')'."
    }

    return $definitions
}

function Get-ListValue {
    param($Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Get-DevicePackDefinitions {
    # Single source of truth: Resources/device-packs.json, generated from the
    # built-in C# catalog (LenovoDeviceSupportProvider). Regenerate with the
    # packdump tool after any catalog change instead of editing this file.
    $definitionsPath = Join-Path $PSScriptRoot '..\Resources\device-packs.json'
    if (-not (Test-Path -LiteralPath $definitionsPath)) {
        throw "Device pack definitions not found at '$definitionsPath'. Regenerate Resources/device-packs.json from the built-in catalog."
    }

    $definitions = Get-Content -LiteralPath $definitionsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    return @($definitions)
}

function Write-StableCatalog {
    param(
        [Parameter(Mandatory = $true)][string]$ReleaseOutputPath,
        [Parameter(Mandatory = $true)][string]$PagesOutputPath
    )

    $resourcesRoot = Join-Path $PagesOutputPath "resources\$Version"
    $languagesCatalogPath = Join-Path $resourcesRoot 'languages\catalog.json'
    $devicesCatalogPath = Join-Path $resourcesRoot 'devices\catalog.json'
    $stableCatalogPath = Join-Path $PagesOutputPath 'stable\catalog.json'
    $resourcesStableCatalogPath = Join-Path $PagesOutputPath 'resources\stable\catalog.json'
    $releaseBaseUrl = "https://github.com/$Repository/releases/download/v$Version"

    if ($IncludeCrossPlatformCli) {
        Assert-CrossPlatformCliReleaseAllowed -ReleaseVersion $Version
    }

    $fullSetupName = Get-FullSetupAssetName $Version
    $onlineSetupName = Get-OnlineSetupAssetName $Version
    $fullZipName = Get-FullZipAssetName $Version
    $onlineZipName = Get-OnlineZipAssetName $Version
    $crossPlatformCliName = Get-CrossPlatformCliAssetName $Version
    $hashName = Get-HashAssetName $Version

    $downloads = [ordered]@{}

    if (Test-Path -LiteralPath (Join-Path $ReleaseOutputPath $fullZipName)) {
        $downloads['full'] = [ordered]@{
            portable = New-FileMetadata `
                -FilePath (Join-Path $ReleaseOutputPath $fullZipName) `
                -Name $fullZipName `
                -CatalogPath "releases/v$Version/$fullZipName" `
                -Url "$releaseBaseUrl/$fullZipName"
        }
    }

    if (Test-Path -LiteralPath (Join-Path $ReleaseOutputPath $onlineZipName)) {
        $downloads['online'] = [ordered]@{
            portable = New-FileMetadata `
                -FilePath (Join-Path $ReleaseOutputPath $onlineZipName) `
                -Name $onlineZipName `
                -CatalogPath "releases/v$Version/$onlineZipName" `
                -Url "$releaseBaseUrl/$onlineZipName"
        }
    }

    if (Test-Path -LiteralPath (Join-Path $ReleaseOutputPath $fullSetupName)) {
        if (-not $downloads.Contains('full')) {
            $downloads['full'] = [ordered]@{}
        }

        $downloads['full']['installer'] = New-FileMetadata `
            -FilePath (Join-Path $ReleaseOutputPath $fullSetupName) `
            -Name $fullSetupName `
            -CatalogPath "releases/v$Version/$fullSetupName" `
            -Url "$releaseBaseUrl/$fullSetupName"
    }

    if (Test-Path -LiteralPath (Join-Path $ReleaseOutputPath $onlineSetupName)) {
        if (-not $downloads.Contains('online')) {
            $downloads['online'] = [ordered]@{}
        }

        $downloads['online']['installer'] = New-FileMetadata `
            -FilePath (Join-Path $ReleaseOutputPath $onlineSetupName) `
            -Name $onlineSetupName `
            -CatalogPath "releases/v$Version/$onlineSetupName" `
            -Url "$releaseBaseUrl/$onlineSetupName"
    }

    if ($IncludeCrossPlatformCli -and (Test-Path -LiteralPath (Join-Path $ReleaseOutputPath $crossPlatformCliName))) {
        $downloads['cli'] = [ordered]@{
            crossPlatform = New-FileMetadata `
                -FilePath (Join-Path $ReleaseOutputPath $crossPlatformCliName) `
                -Name $crossPlatformCliName `
                -CatalogPath "releases/v$Version/$crossPlatformCliName" `
                -Url "$releaseBaseUrl/$crossPlatformCliName"
        }
    }

    $languages = @()
    if (Test-Path -LiteralPath $languagesCatalogPath) {
        $languageCatalog = Get-Content -LiteralPath $languagesCatalogPath -Raw | ConvertFrom-Json
        $languages = @($languageCatalog.languages | ForEach-Object {
            [ordered]@{
                culture = $_.culture
                displayName = $_.displayName
                url = $_.url
                sha256 = $_.sha256
                size = $_.size
            }
        })
    }

    $devicePacks = @()
    if (Test-Path -LiteralPath $devicesCatalogPath) {
        $devicesCatalog = Get-Content -LiteralPath $devicesCatalogPath -Raw | ConvertFrom-Json
        $devicePacks = @($devicesCatalog.devicePacks | ForEach-Object {
            [ordered]@{
                id = $_.id
                displayName = $_.displayName
                vendor = $_.vendor
                vendorAliases = @($_.vendorAliases)
                families = @($_.families)
                modelPrefixes = @($_.modelPrefixes)
                machineTypes = @($_.machineTypes)
                modelKeywords = @($_.modelKeywords)
                url = $_.url
                sha256 = $_.sha256
                size = $_.size
            }
        })
    }

    $sha256 = $null
    if (Test-Path -LiteralPath (Join-Path $ReleaseOutputPath $hashName)) {
        $sha256 = New-FileMetadata `
            -FilePath (Join-Path $ReleaseOutputPath $hashName) `
            -Name $hashName `
            -CatalogPath "releases/v$Version/$hashName" `
            -Url "$releaseBaseUrl/$hashName"
    }

    $catalog = [ordered]@{
        schemaVersion = 1
        appVersion = $Version
        generatedAt = [DateTime]::UtcNow.ToString('o')
        productName = $ProductName
        downloads = $downloads
        sha256 = $sha256
        legacyAliases = @()
        languages = $languages
        devicePacks = $devicePacks
    }

    Write-JsonFile -Value $catalog -Path $stableCatalogPath
    Write-JsonFile -Value $catalog -Path $resourcesStableCatalogPath
}

function Prepare-ReleaseAssets {
    if ([string]::IsNullOrWhiteSpace($BuildDir)) {
        throw 'BuildDir is required unless -FinalizeOnly is specified.'
    }
    if ([string]::IsNullOrWhiteSpace($OnlineBuildDir)) {
        throw 'OnlineBuildDir is required unless -FinalizeOnly is specified.'
    }

    $buildPath = Resolve-RepoPath $BuildDir
    $onlineBuildPath = Resolve-RepoPath $OnlineBuildDir
    $releaseOutputPath = Resolve-RepoPath $ReleaseOutput
    $pagesOutputPath = Resolve-RepoPath $PagesOutput
    $hostBuildPath = if ([string]::IsNullOrWhiteSpace($HostBuildDir)) { $buildPath } else { Resolve-RepoPath $HostBuildDir }

    if (-not (Test-Path -LiteralPath $buildPath)) {
        throw "Build output not found at '$buildPath'."
    }

    if (-not (Test-Path -LiteralPath $hostBuildPath)) {
        throw "Host build output not found at '$hostBuildPath'. Publish UniversalDeviceToolkit.Host before packaging language assets."
    }

    Remove-Item -LiteralPath $onlineBuildPath, $releaseOutputPath, $pagesOutputPath -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $onlineBuildPath, $releaseOutputPath, $pagesOutputPath -Force | Out-Null

    Copy-Item -Path (Join-Path $buildPath '*') -Destination $onlineBuildPath -Recurse -Force

    $languageDirectories = @(Get-LanguageDirectories $buildPath)
    foreach ($directory in $languageDirectories) {
        $target = Join-Path $onlineBuildPath $directory.Name
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Recurse -Force
        }
    }

    $resourcesRoot = Join-Path $pagesOutputPath "resources\$Version"
    $languageOutputPath = Join-Path $resourcesRoot 'languages'
    $deviceOutputPath = Join-Path $resourcesRoot 'devices'
    New-Item -ItemType Directory -Path $languageOutputPath, $deviceOutputPath -Force | Out-Null

    $languageEntries = @()
    $stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) "${PublicAssetPrefix}-lang-assets-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

        foreach ($pack in (Get-LanguagePackDefinitions)) {
            $culture = [string]$pack.Culture
            $sourceDirectories = @($pack.Directories |
                ForEach-Object {
                    $candidate = Join-Path $hostBuildPath $_
                    if ((Test-Path -LiteralPath $candidate) -and (Test-HostSatelliteDirectory $candidate)) {
                        $candidate
                    }
                })

            if ($sourceDirectories.Count -eq 0) {
                throw "Language pack '$culture' cannot be created because no '$culture/UniversalDeviceToolkit.*.resources.dll' Host satellite exists in '$hostBuildPath'. Publish the Host with all supported satellite cultures before packaging."
            }

            $packStage = Join-Path $stagingRoot $culture
            New-Item -ItemType Directory -Path $packStage -Force | Out-Null

            foreach ($sourceDirectory in $sourceDirectories) {
                Copy-Item -LiteralPath $sourceDirectory -Destination $packStage -Recurse -Force
            }

            $packName = Get-LanguageAssetName $Version $culture
            $packZip = Join-Path $languageOutputPath $packName
            Compress-DirectoryContents -SourceDir $packStage -DestinationPath $packZip

            $languageEntries += [ordered]@{
                culture = $culture
                displayName = Get-LanguageDisplayName $culture
                url = "$ResourcesBaseUrl/$Version/languages/$packName"
                sha256 = Get-Sha256Hash -Path $packZip
                size = (Get-Item -LiteralPath $packZip).Length
                asset = New-FileMetadata `
                    -FilePath $packZip `
                    -Name $packName `
                    -CatalogPath "resources/$Version/languages/$packName" `
                    -Url "$ResourcesBaseUrl/$Version/languages/$packName"
                directories = @($sourceDirectories | ForEach-Object { Split-Path -Leaf $_ })
            }
        }
    }
    finally {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    $languageCatalog = [ordered]@{
        schemaVersion = 1
        appVersion = $Version
        generatedAt = [DateTime]::UtcNow.ToString('o')
        productName = $ProductName
        languages = $languageEntries
    }
    Write-JsonFile -Value $languageCatalog -Path (Join-Path $languageOutputPath 'catalog.json')

    $deviceEntries = @()
    $deviceStagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) "${PublicAssetPrefix}-device-assets-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $deviceStagingRoot -Force | Out-Null

        foreach ($pack in (Get-DevicePackDefinitions)) {
            $packId = [string]$pack.Id
            $packStage = Join-Path $deviceStagingRoot $packId
            New-Item -ItemType Directory -Path $packStage -Force | Out-Null

            $manifest = [ordered]@{
                id = $packId
                displayName = [string]$pack.DisplayName
                vendor = [string]$pack.Vendor
                vendorAliases = @(Get-ListValue $pack.VendorAliases)
                families = @(Get-ListValue $pack.Families)
                modelPrefixes = @(Get-ListValue $pack.ModelPrefixes)
                modelKeywords = @(Get-ListValue $pack.ModelKeywords)
                machineTypes = @(Get-ListValue $pack.MachineTypes)
                enabledFeatures = @(Get-ListValue $pack.EnabledFeatures)
                hiddenFeatures = @(Get-ListValue $pack.HiddenFeatures)
            }

            Write-JsonFile -Value $manifest -Path (Join-Path $packStage 'device-pack.json')

            $packName = Get-DeviceAssetName $packId
            $packZip = Join-Path $deviceOutputPath $packName
            Compress-DirectoryContents -SourceDir $packStage -DestinationPath $packZip

            $deviceEntries += [ordered]@{
                id = $packId
                displayName = [string]$pack.DisplayName
                vendor = [string]$pack.Vendor
                vendorAliases = @(Get-ListValue $pack.VendorAliases)
                families = @(Get-ListValue $pack.Families)
                modelPrefixes = @(Get-ListValue $pack.ModelPrefixes)
                modelKeywords = @(Get-ListValue $pack.ModelKeywords)
                machineTypes = @(Get-ListValue $pack.MachineTypes)
                url = "$ResourcesBaseUrl/$Version/devices/$packName"
                sha256 = Get-Sha256Hash -Path $packZip
                size = (Get-Item -LiteralPath $packZip).Length
                asset = New-FileMetadata `
                    -FilePath $packZip `
                    -Name $packName `
                    -CatalogPath "resources/$Version/devices/$packName" `
                    -Url "$ResourcesBaseUrl/$Version/devices/$packName"
            }
        }
    }
    finally {
        Remove-Item -LiteralPath $deviceStagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    $deviceCatalog = [ordered]@{
        schemaVersion = 1
        appVersion = $Version
        generatedAt = [DateTime]::UtcNow.ToString('o')
        productName = $ProductName
        devicePacks = $deviceEntries
    }
    Write-JsonFile -Value $deviceCatalog -Path (Join-Path $deviceOutputPath 'catalog.json')

    Write-StableCatalog -ReleaseOutputPath $releaseOutputPath -PagesOutputPath $pagesOutputPath

    Write-Host "Prepared language and device resources in '$pagesOutputPath'. Electron packaging supplies the desktop ZIP assets."
    Write-Host "Prepared Online build output in '$onlineBuildPath'."
    Write-Host "Prepared GitHub Pages resources in '$pagesOutputPath'."
}

function Finalize-ReleaseAssets {
    if ($IncludeCrossPlatformCli) {
        Assert-CrossPlatformCliReleaseAllowed -ReleaseVersion $Version
    }

    if ([string]::IsNullOrWhiteSpace($FullInstallerPath)) {
        throw 'FullInstallerPath is required with -FinalizeOnly.'
    }
    if ([string]::IsNullOrWhiteSpace($OnlineInstallerPath)) {
        throw 'OnlineInstallerPath is required with -FinalizeOnly.'
    }
    if ([string]::IsNullOrWhiteSpace($FullZipPath) -or [string]::IsNullOrWhiteSpace($OnlineZipPath)) {
        throw 'FullZipPath and OnlineZipPath are required with -FinalizeOnly.'
    }

    $releaseOutputPath = Resolve-RepoPath $ReleaseOutput
    $pagesOutputPath = Resolve-RepoPath $PagesOutput
    $fullInstallerSource = Resolve-RepoPath $FullInstallerPath
    $onlineInstallerSource = Resolve-RepoPath $OnlineInstallerPath
    $fullZipSource = Resolve-RepoPath $FullZipPath
    $onlineZipSource = Resolve-RepoPath $OnlineZipPath

    if (-not (Test-Path -LiteralPath $fullInstallerSource)) {
        throw "Full installer not found at '$fullInstallerSource'."
    }
    if (-not (Test-Path -LiteralPath $onlineInstallerSource)) {
        throw "Online installer not found at '$onlineInstallerSource'."
    }
    if (-not (Test-Path -LiteralPath $fullZipSource)) {
        throw "Full Electron ZIP not found at '$fullZipSource'."
    }
    if (-not (Test-Path -LiteralPath $onlineZipSource)) {
        throw "Online Electron ZIP not found at '$onlineZipSource'."
    }

    New-Item -ItemType Directory -Path $releaseOutputPath, $pagesOutputPath -Force | Out-Null

    $fullSetupName = Get-FullSetupAssetName $Version
    $onlineSetupName = Get-OnlineSetupAssetName $Version
    $fullZipName = Get-FullZipAssetName $Version
    $onlineZipName = Get-OnlineZipAssetName $Version
    $hashName = Get-HashAssetName $Version

    Copy-Item -LiteralPath $fullInstallerSource -Destination (Join-Path $releaseOutputPath $fullSetupName) -Force
    Copy-Item -LiteralPath $onlineInstallerSource -Destination (Join-Path $releaseOutputPath $onlineSetupName) -Force
    Copy-Item -LiteralPath $fullZipSource -Destination (Join-Path $releaseOutputPath $fullZipName) -Force
    Copy-Item -LiteralPath $onlineZipSource -Destination (Join-Path $releaseOutputPath $onlineZipName) -Force

    $hashAssetNames = @($fullSetupName, $onlineSetupName, $fullZipName, $onlineZipName)
    $installerDir = Split-Path -Parent $onlineInstallerSource
    Get-ChildItem -LiteralPath $installerDir -Filter '*.nsis.7z' -ErrorAction SilentlyContinue |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $releaseOutputPath $_.Name) -Force
            $hashAssetNames += $_.Name
        }
    if ($IncludeCrossPlatformCli) {
        $crossPlatformCliName = Get-CrossPlatformCliAssetName $Version
        $crossPlatformCliPath = Join-Path $releaseOutputPath $crossPlatformCliName
        if (-not (Test-Path -LiteralPath $crossPlatformCliPath)) {
            throw "Cross-platform CLI asset not found at '$crossPlatformCliPath'. Release finalization requires the macOS/Linux diagnostics package."
        }

        $hashAssetNames += $crossPlatformCliName
    }

    Write-HashFile -AssetNames $hashAssetNames -ReleaseOutputPath $releaseOutputPath -HashFileName $hashName
    Write-StableCatalog -ReleaseOutputPath $releaseOutputPath -PagesOutputPath $pagesOutputPath

    Write-Host "Finalized Electron installers, portable ZIPs, and SHA256 file in '$releaseOutputPath'."
    Write-Host "Finalized stable catalogs in '$pagesOutputPath\stable\catalog.json' and '$pagesOutputPath\resources\stable\catalog.json'."
}

if ($IncludeCrossPlatformCli) {
    Assert-CrossPlatformCliReleaseAllowed -ReleaseVersion $Version
}

if ($FinalizeOnly) {
    Finalize-ReleaseAssets
} else {
    Prepare-ReleaseAssets
}
