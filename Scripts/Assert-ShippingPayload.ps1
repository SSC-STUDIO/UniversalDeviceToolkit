param(
    [Parameter(Mandatory = $true)]
    [string]$PayloadPath
)

$ErrorActionPreference = 'Stop'

$resolvedPath = Resolve-Path -LiteralPath $PayloadPath -ErrorAction SilentlyContinue
if (-not $resolvedPath) {
    Write-Error "Shipping payload directory not found: $PayloadPath"
    exit 1
}

$pathTrimChars = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$payloadRoot = $resolvedPath.Path.TrimEnd($pathTrimChars)

$forbiddenExactNames = @(
    'SpectrumTester.exe',
    'SpectrumTester.dll',
    'SpectrumTester.deps.json',
    'SpectrumTester.runtimeconfig.json'
)

$forbiddenNamePrefixes = @(
    'UniversalDeviceToolkit.Tests',
    'UniversalDeviceToolkit.CrossPlatform.Tests',
    'UniversalDeviceToolkit.PerformanceTest',
    'LanguagePackUi.Smoke',
    'LanguagePackInstallProgressSmoke',
    'VisualRegression.Smoke',
    'HardwareValidation',
    'PresetUiValidation',
    'SensorInventoryDump',
    'testhost',
    'xunit.'
)

$forbiddenPathSegments = @(
    'Tools',
    'Tests',
    'x86',
    'arm64'
)

$forbiddenNamePatterns = @(
    '*.Tests.*',
    '*.Smoke.*',
    '*Validation*',
    '*TestHost*',
    '*.pdb'
)

$forbiddenBinaryMarkers = @(
    'UDT_APPDATA_OVERRIDE'
)

# LocalizationRuntime intentionally keeps this test-only app-data override in
# the portable abstractions assembly so resource-contract tests can isolate
# their files. It is not a shipping test artifact when present in that known
# runtime assembly; keep scanning every other payload file for the marker.
$allowedBinaryMarkerNames = @(
    'UniversalDeviceToolkit.Lib.Abstractions.dll'
)

function Test-ContainsBytes {
    param(
        [Parameter(Mandatory = $true)][byte[]]$Haystack,
        [Parameter(Mandatory = $true)][byte[]]$Needle
    )

    if ($Needle.Length -eq 0 -or $Haystack.Length -lt $Needle.Length) {
        return $false
    }

    for ($i = 0; $i -le $Haystack.Length - $Needle.Length; $i++) {
        $matched = $true
        for ($j = 0; $j -lt $Needle.Length; $j++) {
            if ($Haystack[$i + $j] -ne $Needle[$j]) {
                $matched = $false
                break
            }
        }

        if ($matched) {
            return $true
        }
    }

    return $false
}

function Test-ContainsBinaryMarker {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Marker
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $encodedMarkers = @(
        [System.Text.Encoding]::UTF8.GetBytes($Marker),
        [System.Text.Encoding]::Unicode.GetBytes($Marker),
        [System.Text.Encoding]::BigEndianUnicode.GetBytes($Marker)
    )

    foreach ($encodedMarker in $encodedMarkers) {
        if (Test-ContainsBytes -Haystack $bytes -Needle $encodedMarker) {
            return $true
        }
    }

    return $false
}

$violations = @()
$files = Get-ChildItem -LiteralPath $resolvedPath.Path -Recurse -File -ErrorAction Stop
foreach ($file in $files) {
    $isForbidden = $false

    $relativePath = $file.FullName
    if ($relativePath.StartsWith($payloadRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $relativePath = $relativePath.Substring($payloadRoot.Length).TrimStart($pathTrimChars)
    }

    $pathSegments = $relativePath -split '[\\/]'
    foreach ($segment in $pathSegments) {
        foreach ($forbiddenSegment in $forbiddenPathSegments) {
            if ([string]::Equals($segment, $forbiddenSegment, [StringComparison]::OrdinalIgnoreCase)) {
                $isForbidden = $true
                break
            }
        }

        if ($isForbidden) { break }
    }

    if ($isForbidden) {
        $violations += $file
        continue
    }

    foreach ($forbiddenName in $forbiddenExactNames) {
        if ([string]::Equals($file.Name, $forbiddenName, [StringComparison]::OrdinalIgnoreCase)) {
            $isForbidden = $true
            break
        }
    }

    if ($isForbidden) {
        $violations += $file
        continue
    }

    foreach ($prefix in $forbiddenNamePrefixes) {
        if ($file.Name.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $isForbidden = $true
            $violations += $file
            break
        }
    }

    if ($isForbidden) {
        continue
    }

    foreach ($pattern in $forbiddenNamePatterns) {
        if ($file.Name -like $pattern) {
            $isForbidden = $true
            $violations += $file
            break
        }
    }

    if (-not $isForbidden -and $allowedBinaryMarkerNames -notcontains $file.Name) {
        foreach ($marker in $forbiddenBinaryMarkers) {
            if (Test-ContainsBinaryMarker -Path $file.FullName -Marker $marker) {
                $violations += $file
                break
            }
        }
    }
}

if ($violations.Count -gt 0) {
    [Console]::Error.WriteLine("Shipping payload contains test or validation tool artifacts:")
    foreach ($violation in $violations | Sort-Object FullName) {
        [Console]::Error.WriteLine(" - $($violation.FullName)")
    }

    exit 1
}

Write-Host "Shipping payload validation passed: no test or validation tool artifacts found in $($resolvedPath.Path)"
