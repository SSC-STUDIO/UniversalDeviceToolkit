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
    'UniversalDeviceToolkit.Lib.Plugins',
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

# Compile once per PowerShell session. Large runtime assemblies must not be
# scanned byte-by-byte by the PowerShell interpreter.
if (-not ('UniversalDeviceToolkit.Packaging.BinaryMarkerSearch' -as [type])) {
    Add-Type -TypeDefinition @'
using System;

namespace UniversalDeviceToolkit.Packaging
{
    public static class BinaryMarkerSearch
    {
        public static bool Contains(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
                return false;

            int lastStart = haystack.Length - needle.Length;
            int start = 0;
            while (start <= lastStart)
            {
                int candidate = Array.IndexOf(haystack, needle[0], start, lastStart - start + 1);
                if (candidate < 0)
                    return false;

                int index = 1;
                while (index < needle.Length && haystack[candidate + index] == needle[index])
                    index++;

                if (index == needle.Length)
                    return true;

                start = candidate + 1;
            }

            return false;
        }
    }
}
'@
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
        if ([UniversalDeviceToolkit.Packaging.BinaryMarkerSearch]::Contains($bytes, $encodedMarker)) {
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
