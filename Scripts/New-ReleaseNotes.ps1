param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $true)]
  [string]$ChangelogPath,

  [string[]]$AssetNames = @(),

  [string]$ProductName = 'Universal Device Toolkit',

  [string]$ReleaseDate,

  [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Get-ReleaseSection {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseVersion
  )

  # Always read changelog as UTF-8 (no BOM). Default Get-Content encoding is locale-dependent on Windows.
  $lines = [System.IO.File]::ReadAllLines($Path, [System.Text.UTF8Encoding]::new($false))
  $versionPattern = '^## \[' + [regex]::Escape($ReleaseVersion) + '\] - (?<date>\d{4}-\d{2}-\d{2})$'
  $startIndex = -1
  $endIndex = $lines.Count
  $releaseDate = $null

  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $versionPattern) {
      $startIndex = $i + 1
      $releaseDate = $Matches['date']
      break
    }
  }

  if ($startIndex -lt 0 -or [string]::IsNullOrWhiteSpace($releaseDate)) {
    return $null
  }

  for ($i = $startIndex; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^## \[') {
      $endIndex = $i
      break
    }
  }

  $section = ''
  if ($endIndex -gt $startIndex) {
    $section = ($lines[$startIndex..($endIndex - 1)] -join "`n").Trim()
  }

  [pscustomobject]@{
    Date = $releaseDate
    Body = $section
  }
}

function Get-SectionSummary {
  param(
    [string]$Body
  )

  if ([string]::IsNullOrWhiteSpace($Body)) {
    return @('- Release notes were not available in the repository changelog for this historical version.')
  }

  $items = New-Object System.Collections.Generic.List[string]
  $lines = @($Body -split "`r?`n")
  $startIndex = 0
  $endIndex = $lines.Count

  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^###\s+Highlights\b') {
      $startIndex = $i + 1
      for ($j = $startIndex; $j -lt $lines.Count; $j++) {
        if ($lines[$j] -match '^###\s+') {
          $endIndex = $j
          break
        }
      }
      break
    }
  }

  for ($i = $startIndex; $i -lt $endIndex; $i++) {
    $line = $lines[$i]
    $trimmed = $line.Trim()
    if (-not $trimmed.StartsWith('- ')) {
      continue
    }

    $items.Add($trimmed)
    if ($items.Count -ge 5) {
      break
    }
  }

  if ($items.Count -eq 0) {
    return @('- See the detailed changes section below.')
  }

  return $items.ToArray()
}

function Remove-LeadingHighlightsSection {
  param(
    [string]$Body
  )

  if ([string]::IsNullOrWhiteSpace($Body)) {
    return ''
  }

  $lines = @($Body -split "`r?`n")
  $index = 0
  while ($index -lt $lines.Count -and [string]::IsNullOrWhiteSpace($lines[$index])) {
    $index++
  }

  if ($index -ge $lines.Count -or $lines[$index] -notmatch '^###\s+Highlights\b') {
    return $Body
  }

  $index++
  while ($index -lt $lines.Count -and $lines[$index] -notmatch '^###\s+') {
    $index++
  }

  if ($index -ge $lines.Count) {
    return ''
  }

  return (($lines[$index..($lines.Count - 1)] -join "`n").Trim())
}

function Add-AssetLine {
  param(
    [System.Collections.Generic.List[string]]$Lines,
    [string]$AssetName,
    [string]$Description
  )

  $Lines.Add("- ``$AssetName`` - $Description")
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
    [Parameter(Mandatory = $true)][string]$ReleaseVersion,
    [Parameter(Mandatory = $true)][string[]]$Names
  )

  $hasCrossPlatformCli = @($Names | Where-Object { $_ -match '_CLI_cross-platform\.zip$' }).Count -gt 0
  if ($hasCrossPlatformCli -and (Get-MajorVersion -Value $ReleaseVersion -Name 'Version') -lt 5) {
    throw "Cross-platform CLI assets are not published before 5.x.x. Version '$ReleaseVersion' was requested."
  }
}

function Get-DownloadLines {
  param(
    [string]$ReleaseVersion,
    [string[]]$Names
  )

  $lines = New-Object System.Collections.Generic.List[string]
  $sorted = @($Names | Sort-Object)

  $fullSetup = $sorted | Where-Object { $_ -match '_Full_Setup\.exe$' } | Select-Object -First 1
  $webView2Setup = $sorted | Where-Object { $_ -match '^UniversalDeviceToolkitWebView2Setup-.+\.exe$' } | Select-Object -First 1
  $onlineSetup = $sorted | Where-Object { $_ -match '_Online_Setup\.exe$' } | Select-Object -First 1
  $fullZip = $sorted | Where-Object { $_ -match '_Full_win-x64\.zip$' } | Select-Object -First 1
  $onlineZip = $sorted | Where-Object { $_ -match '_Online_win-x64\.zip$' } | Select-Object -First 1
  $crossPlatformCliZip = $sorted | Where-Object { $_ -match '_CLI_cross-platform\.zip$' } | Select-Object -First 1
  $englishSetup = $sorted | Where-Object { $_ -match '_English_Setup\.exe$' } | Select-Object -First 1
  $englishZip = $sorted | Where-Object { $_ -match '_English_win-x64\.zip$' } | Select-Object -First 1
  $setup = $sorted | Where-Object { $_ -match 'Setup\.exe$' -and $_ -ne $fullSetup -and $_ -ne $onlineSetup -and $_ -ne $englishSetup } | Select-Object -First 1
  $zip = $sorted | Where-Object { $_ -match '_win-x64\.zip$' -and $_ -ne $fullZip -and $_ -ne $onlineZip -and $_ -ne $englishZip } | Select-Object -First 1
  $sha = $sorted | Where-Object { $_ -match '_SHA256\.txt$' } | Select-Object -First 1
  $languagePacks = @($sorted | Where-Object { $_ -match '_lang_[^/]+\.zip$' })

  if ($webView2Setup) {
    Add-AssetLine $lines $webView2Setup 'Windows WebView2 installer with all languages and the .NET runtime. Microsoft Edge WebView2 Runtime must already be installed.'
    if ($fullSetup) { Add-AssetLine $lines $fullSetup 'Same WebView2 installer under the legacy Full update-channel name.' }
    if ($onlineSetup) { Add-AssetLine $lines $onlineSetup 'Same WebView2 installer under the legacy Online update-channel name.' }
    if ($fullZip) { Add-AssetLine $lines $fullZip 'WebView2 portable application with all languages; requires system WebView2 Runtime.' }
    if ($onlineZip) { Add-AssetLine $lines $onlineZip 'Same WebView2 portable application under the legacy Online name.' }
  } else {
    if ($fullSetup) { Add-AssetLine $lines $fullSetup 'Full installer with bundled languages and device support data.' }
    if ($onlineSetup) { Add-AssetLine $lines $onlineSetup 'Online installer with the base app; additional language and device resources install from the in-app online catalog.' }
    if ($fullZip) { Add-AssetLine $lines $fullZip 'Full portable package with bundled languages and device support data.' }
    if ($onlineZip) { Add-AssetLine $lines $onlineZip 'Online portable package with the base app; additional resources install from the in-app online catalog.' }
  }
  if ($crossPlatformCliZip) { Add-AssetLine $lines $crossPlatformCliZip 'Framework-dependent diagnostics CLI for Windows, macOS, and Linux. Includes `udt.cmd`, `udt`, and `README.txt` launch guidance; `dotnet udt.dll <command>` still works on any OS.' }
  if ($englishSetup) { Add-AssetLine $lines $englishSetup 'Legacy online-style installer asset; prefer the Online installer for current releases.' }
  if ($englishZip) { Add-AssetLine $lines $englishZip 'Legacy online-style portable asset; prefer the Online portable package for current releases.' }
  if ($setup) { Add-AssetLine $lines $setup 'installer package.' }
  if ($zip) { Add-AssetLine $lines $zip 'portable win-x64 package.' }
  if ($languagePacks.Count -gt 0) { $lines.Add("- ``*_lang_<culture>.zip`` - legacy language-pack assets for historical releases ($($languagePacks.Count) assets). Current releases use the GitHub Pages language catalog.") }
  if ($sha) { Add-AssetLine $lines $sha 'SHA256 checksum manifest.' }

  if ($lines.Count -eq 0) {
    $lines.Add('- No release assets are currently attached to this historical release.')
  }

  return $lines.ToArray()
}

function Get-OnlineResourceLines {
  param(
    [string]$ReleaseVersion,
    [string[]]$Names
  )

  $hasUniversalAssets = @($Names | Where-Object { $_ -like 'UniversalDeviceToolkit_*' }).Count -gt 0
  $hasLanguagePackAssets = @($Names | Where-Object { $_ -match '_lang_[^/]+\.zip$' }).Count -gt 0

  if ($hasUniversalAssets) {
    return @(
      "- Language resources are published through GitHub Pages under ``resources/$ReleaseVersion/languages``.",
      "- Device model resources are published through GitHub Pages under ``resources/$ReleaseVersion/devices``.",
      '- The stable online catalog is published at `resources/stable/catalog.json`; `stable/catalog.json` remains as a compatibility copy.'
    )
  }

  if ($hasLanguagePackAssets) {
    return @(
      '- Optional language packs are attached directly to this historical GitHub Release.',
      '- Device packs and the GitHub Pages resource catalog were introduced later in the Universal Device Toolkit release train.'
    )
  }

  return @(
    '- This historical release does not use the GitHub Pages language/device resource catalog.'
  )
}

function Get-VerificationLines {
  param(
    [string[]]$Names
  )

  $sha = @($Names | Where-Object { $_ -match '_SHA256\.txt$' }).Count -gt 0
  if ($sha) {
    return @(
      '- Download the SHA256 manifest attached to this release.',
      '- Verify any downloaded asset with `CertUtil -hashfile <file> SHA256` and compare it with the manifest.'
    )
  }

  return @(
    '- This historical release does not include a SHA256 manifest asset; verify the download source and prefer newer releases when possible.'
  )
}

function Get-CompatibilityLines {
  param(
    [string[]]$Names
  )

  $lines = New-Object System.Collections.Generic.List[string]
  $hasCrossPlatformCli = @($Names | Where-Object { $_ -match '_CLI_cross-platform\.zip$' }).Count -gt 0

  $lines.Add('- Desktop app and hardware controls: Windows 10/11 x64')
  if ($hasCrossPlatformCli) {
    $lines.Add('- Cross-platform diagnostics CLI: Windows, macOS, and Linux with .NET 10 runtime')
  }

  return $lines.ToArray()
}

$section = Get-ReleaseSection -Path $ChangelogPath -ReleaseVersion $Version
$releaseDate = if ($section) { $section.Date } elseif (-not [string]::IsNullOrWhiteSpace($ReleaseDate)) { $ReleaseDate } else { 'Unknown' }
$changeBody = if ($section) { $section.Body } else { '' }
Assert-CrossPlatformCliReleaseAllowed -ReleaseVersion $Version -Names $AssetNames
$highlights = Get-SectionSummary -Body $changeBody
$changesBody = Remove-LeadingHighlightsSection -Body $changeBody
$downloads = Get-DownloadLines -ReleaseVersion $Version -Names $AssetNames
$onlineResources = Get-OnlineResourceLines -ReleaseVersion $Version -Names $AssetNames
$compatibility = Get-CompatibilityLines -Names $AssetNames
$verification = Get-VerificationLines -Names $AssetNames

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# $ProductName v$Version")
$lines.Add('')
$lines.Add("Release date: $releaseDate")
$lines.Add('')
$lines.Add('## Highlights')
$lines.AddRange([string[]]$highlights)
$lines.Add('')
$lines.Add('## Changes')
if ([string]::IsNullOrWhiteSpace($changesBody)) {
  $lines.Add('- No changelog section is available for this historical version.')
} else {
  $lines.Add($changesBody)
}
$lines.Add('')
$lines.Add('## Downloads')
$lines.AddRange([string[]]$downloads)
$lines.Add('')
$lines.Add('## Online resources')
$lines.AddRange([string[]]$onlineResources)
$lines.Add('')
$lines.Add('## Compatibility')
$lines.AddRange([string[]]$compatibility)
$lines.Add('')
$lines.Add('## Verification')
$lines.AddRange([string[]]$verification)

$notes = ($lines -join "`n").Trim() + "`n"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
  Write-Output $notes
} else {
  $parent = Split-Path -Parent $OutputPath
  if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
  }
  [System.IO.File]::WriteAllText($OutputPath, $notes, [System.Text.UTF8Encoding]::new($false))
}
