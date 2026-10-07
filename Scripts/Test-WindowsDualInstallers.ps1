[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WebView2Installer,
    [Parameter(Mandatory = $true)][string]$CompatibilityInstaller,
    [Parameter(Mandatory = $true)][string]$HashManifest,
    [string]$ReportDirectory = 'TestResults/dual-installers',
    [ValidateRange(30, 1800)][int]$TimeoutSeconds = 300,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
$uninstallRoot = 'Software\Microsoft\Windows\CurrentVersion\Uninstall'
$productKey = 'UniversalDeviceToolkit'
$views = @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-CanonicalPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\', '/'))
}

function Assert-NoLinks([string]$Path) {
    for ($current = Get-CanonicalPath $Path; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        if (Test-Path -LiteralPath $current) {
            $attributes = [IO.File]::GetAttributes($current)
            Assert-Condition (($attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "The test path contains a reparse point: $current"
        }
    }
}

function Get-RegistrySnapshot([Microsoft.Win32.RegistryKey]$Key) {
    $values = @{}
    foreach ($name in $Key.GetValueNames()) {
        $values[$name] = @{
            Kind = [int]$Key.GetValueKind($name)
            Value = $Key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        }
    }
    $children = @{}
    foreach ($name in $Key.GetSubKeyNames()) {
        $child = $Key.OpenSubKey($name)
        try { $children[$name] = Get-RegistrySnapshot $child }
        finally { $child.Dispose() }
    }
    return @{ Values = $values; Children = $children }
}

function Write-RegistrySnapshot([Microsoft.Win32.RegistryKey]$Key, [hashtable]$Snapshot) {
    foreach ($name in $Snapshot.Values.Keys) {
        $value = $Snapshot.Values[$name]
        $Key.SetValue($name, $value.Value, [Microsoft.Win32.RegistryValueKind]$value.Kind)
    }
    foreach ($name in $Snapshot.Children.Keys) {
        $child = $Key.CreateSubKey($name)
        try { Write-RegistrySnapshot $child $Snapshot.Children[$name] }
        finally { $child.Dispose() }
    }
}

function Get-UninstallBackup {
    $entries = @()
    foreach ($view in $views) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
        try {
            $uninstall = $root.OpenSubKey($uninstallRoot)
            try {
                $names = @($productKey)
                if ($null -ne $uninstall) {
                    foreach ($name in $uninstall.GetSubKeyNames()) {
                        $key = $uninstall.OpenSubKey($name)
                        try {
                            if ($key.GetValue('DisplayName') -eq 'Universal Device Toolkit') { $names += $name }
                        }
                        finally { $key.Dispose() }
                    }
                }
                foreach ($name in ($names | Select-Object -Unique)) {
                    $key = $root.OpenSubKey("$uninstallRoot\$name")
                    try {
                        $snapshot = if ($null -eq $key) { $null } else { Get-RegistrySnapshot $key }
                        $entries += @{ View = [int]$view; Name = $name; Snapshot = $snapshot }
                    }
                    finally { if ($null -ne $key) { $key.Dispose() } }
                }
            }
            finally { if ($null -ne $uninstall) { $uninstall.Dispose() } }
        }
        finally { $root.Dispose() }
    }
    return ,$entries
}

function Restore-UninstallBackup([object[]]$Entries) {
    $failures = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in $Entries) {
        try {
            $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]$entry.View)
            try {
                $path = "$uninstallRoot\$($entry.Name)"
                $root.DeleteSubKeyTree($path, $false)
                if ($null -ne $entry.Snapshot) {
                    $key = $root.CreateSubKey($path)
                    try { Write-RegistrySnapshot $key $entry.Snapshot }
                    finally { $key.Dispose() }
                }
            }
            finally { $root.Dispose() }
        }
        catch { $failures.Add($_.Exception.ToString()) }
    }
    if ($failures.Count -gt 0) { throw ($failures -join '; ') }
}

function Get-VerifiedPackage([string]$Path, [string]$Pattern, [string]$Manifest) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $name = [IO.Path]::GetFileName($resolved)
    Assert-Condition ($name -match $Pattern) "Unexpected installer name: $name"
    $hashes = @()
    foreach ($line in [IO.File]::ReadAllLines($Manifest)) {
        if ($line.Trim() -match '^([a-fA-F0-9]{64})\s+\*?(.+)$' -and $Matches[2] -eq $name) {
            $hashes += $Matches[1].ToLowerInvariant()
        }
    }
    $hashes = @($hashes | Select-Object -Unique)
    Assert-Condition ($hashes.Count -eq 1) "The manifest must contain one unambiguous SHA256 entry for $name"
    $actual = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Condition ($actual -eq $hashes[0]) "Installer SHA256 mismatch: $name"
    return @{ Path = $resolved; Name = $name; Hash = $actual; Bytes = (Get-Item -LiteralPath $resolved).Length }
}

function Invoke-Installer([hashtable]$Package) {
    Assert-NoLinks $installation
    Assert-Condition ((Get-FileHash -LiteralPath $Package.Path -Algorithm SHA256).Hash -eq $Package.Hash) 'The installer changed after verification.'
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Package.Path
    # NSIS requires /D= last and unquoted, including a destination with spaces.
    $start.Arguments = "/S /D=$installation"
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            & taskkill.exe /PID $process.Id /T /F | Out-Null
            Assert-Condition ($LASTEXITCODE -eq 0) 'Unable to stop the installer process tree before restoring system metadata.'
            $process.WaitForExit()
            throw "Installer timed out: $($Package.Name)"
        }
        Assert-Condition ($process.ExitCode -eq 0) "Installer failed ($($process.ExitCode)): $($Package.Name)"
    }
    finally { $process.Dispose() }
}

function Assert-Installation([string]$Channel) {
    Assert-NoLinks $installation
    $marker = [IO.File]::ReadAllText((Join-Path $installation 'resources/install-channel')).Trim()
    Assert-Condition ($marker -eq $Channel) "Expected $Channel, found $marker"
    $owned = @([IO.File]::ReadAllText((Join-Path $installation 'resources/install-files.json')) | ConvertFrom-Json)
    foreach ($relative in $owned) {
        Assert-Condition (-not [IO.Path]::IsPathRooted($relative)) 'An ownership path is absolute.'
        $path = Get-CanonicalPath (Join-Path $installation $relative)
        Assert-Condition ($path.StartsWith($installation + '\', [StringComparison]::OrdinalIgnoreCase)) 'An ownership path escapes the test installation.'
        Assert-NoLinks $path
        Assert-Condition ([IO.File]::Exists($path)) "Installed owned file is missing: $relative"
    }
    $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $key = $root.OpenSubKey("$uninstallRoot\$productKey")
        Assert-Condition ($null -ne $key) 'The common uninstall registration is missing.'
        try { Assert-Condition ((Get-CanonicalPath $key.GetValue('InstallLocation')) -eq $installation) 'The installer registered a different destination.' }
        finally { $key.Dispose() }
    }
    finally { $root.Dispose() }
    foreach ($shortcut in $shortcutBackup) {
        Assert-Condition ([IO.File]::Exists($shortcut.Path)) "The installed shortcut is missing: $($shortcut.Path)"
        $shell = New-Object -ComObject WScript.Shell
        try {
            $target = $shell.CreateShortcut($shortcut.Path).TargetPath
            Assert-Condition ((Get-CanonicalPath $target) -eq (Join-Path $installation 'UniversalDeviceToolkit.exe')) 'The shortcut targets a different installation.'
        }
        finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
    }
}

function Assert-PreservedData {
    Assert-Condition ([IO.File]::ReadAllText($unowned) -eq 'unowned-installation-sentinel') 'The installer removed an unowned file.'
    Assert-Condition ([IO.File]::ReadAllText($settings) -eq $settingsContent) 'The installer changed the isolated settings file.'
}

function Test-UninstallCompleted {
    if ([IO.File]::Exists($uninstaller)) { return $false }
    foreach ($relative in $owned) {
        if ([IO.File]::Exists((Join-Path $installation $relative))) { return $false }
    }
    foreach ($shortcut in $shortcutBackup) {
        if ([IO.File]::Exists($shortcut.Path)) { return $false }
    }
    $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]::Registry64)
    try {
        $key = $root.OpenSubKey("$uninstallRoot\$productKey")
        if ($null -eq $key) { return $true }
        $key.Dispose()
        return $false
    }
    finally { $root.Dispose() }
}

Assert-Condition ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) 'This verification requires Windows.'
$manifest = (Resolve-Path -LiteralPath $HashManifest).Path
$primary = Get-VerifiedPackage $WebView2Installer '^UniversalDeviceToolkitWebView2Setup-\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?\.exe$' $manifest
$compatibility = Get-VerifiedPackage $CompatibilityInstaller '^UniversalDeviceToolkitCompatibilitySetup-\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?\.exe$' $manifest
Assert-Condition ($primary.Bytes -le 40000000) 'The WebView2 installer exceeds 40,000,000 bytes.'
Assert-Condition ($compatibility.Bytes -le 185 * 1024 * 1024) 'The compatibility installer exceeds 185 MiB.'
$primaryVersion = $primary.Name -replace '^UniversalDeviceToolkitWebView2Setup-|\.exe$', ''
$compatibilityVersion = $compatibility.Name -replace '^UniversalDeviceToolkitCompatibilitySetup-|\.exe$', ''
Assert-Condition ($primaryVersion -eq $compatibilityVersion) 'Both installers must be from the same candidate version.'
if ($ValidateOnly) { Write-Output 'Installer names, SHA256 entries and package budgets passed.'; return }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
Assert-Condition ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 'Run verification from an elevated PowerShell terminal.'
$report = if ([IO.Path]::IsPathRooted($ReportDirectory)) { Get-CanonicalPath $ReportDirectory } else { Get-CanonicalPath (Join-Path $repository $ReportDirectory) }
Assert-NoLinks $report
Assert-Condition (-not [IO.File]::Exists((Join-Path $report 'system-backup.clixml'))) 'Choose a fresh report directory to preserve any previous recovery backup.'
$null = New-Item -ItemType Directory -Path $report -Force
$temporaryRoot = Get-CanonicalPath (Join-Path ([IO.Path]::GetTempPath()) ('udt-dual-installers-' + [Guid]::NewGuid().ToString('N')))
Assert-NoLinks $temporaryRoot
Assert-Condition (-not (Test-Path -LiteralPath $temporaryRoot)) 'The temporary test root already exists.'
$installation = Join-Path $temporaryRoot 'isolated installation with spaces'
$appdata = Join-Path $temporaryRoot 'isolated appdata'
$registryBackup = Get-UninstallBackup
foreach ($entry in $registryBackup) {
    if ($null -ne $entry.Snapshot -and $entry.Snapshot.Values.ContainsKey('InstallLocation')) {
        $previous = [string]$entry.Snapshot.Values['InstallLocation'].Value
        if ($previous) {
            Assert-Condition ((Get-CanonicalPath $previous) -ne $installation) 'The test destination matches an existing installation.'
        }
    }
}
$shortcutPaths = @(
    (Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'Universal Device Toolkit.lnk'),
    (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Universal Device Toolkit/Universal Device Toolkit.lnk')
)
$shortcutBackup = @($shortcutPaths | ForEach-Object {
    Assert-NoLinks $_
    @{ Path = $_; Bytes = $(if ([IO.File]::Exists($_)) { [IO.File]::ReadAllBytes($_) } else { $null });
        ParentExisted = [IO.Directory]::Exists([IO.Path]::GetDirectoryName($_)) }
})
@{ Registry = $registryBackup; Shortcuts = $shortcutBackup } | Export-Clixml -LiteralPath (Join-Path $report 'system-backup.clixml')
$previousOverride = [Environment]::GetEnvironmentVariable('UDT_APPDATA_OVERRIDE', 'Process')
$results = New-Object 'System.Collections.Generic.List[object]'
$failure = $null
$recoveryFailures = New-Object 'System.Collections.Generic.List[string]'
try {
    $null = New-Item -ItemType Directory -Path $temporaryRoot, $appdata
    $env:UDT_APPDATA_OVERRIDE = $appdata
    $settings = Join-Path $appdata 'dashboard.json'
    $settingsContent = '{"installationVerification":"settings-must-survive"}'
    [IO.File]::WriteAllText($settings, $settingsContent, $utf8)
    Invoke-Installer $primary
    Assert-Installation 'webview2'
    $results.Add(@{ Step = 'clean-webview2-install'; Passed = $true })
    $unowned = Join-Path $installation 'user-owned-sentinel.txt'
    [IO.File]::WriteAllText($unowned, 'unowned-installation-sentinel', $utf8)
    Invoke-Installer $primary
    Assert-Installation 'webview2'
    Assert-PreservedData
    $results.Add(@{ Step = 'same-channel-webview2-reinstall'; Passed = $true })
    Invoke-Installer $compatibility
    Assert-Installation 'electron-compatibility'
    Assert-PreservedData
    $results.Add(@{ Step = 'webview2-to-electron'; Passed = $true })
    Invoke-Installer $compatibility
    Assert-Installation 'electron-compatibility'
    Assert-PreservedData
    $results.Add(@{ Step = 'same-channel-electron-reinstall'; Passed = $true })
    Invoke-Installer $primary
    Assert-Installation 'webview2'
    Assert-PreservedData
    $results.Add(@{ Step = 'electron-to-webview2'; Passed = $true })
    $owned = @([IO.File]::ReadAllText((Join-Path $installation 'resources/install-files.json')) | ConvertFrom-Json)
    $uninstaller = Join-Path $installation 'Uninstall.exe'
    $process = Start-Process -FilePath $uninstaller -ArgumentList '/S' -PassThru -WindowStyle Hidden
    try {
        Assert-Condition ($process.WaitForExit($TimeoutSeconds * 1000)) 'The uninstaller launcher timed out.'
        $results.Add(@{ Step = 'uninstaller-launcher'; Passed = ($process.ExitCode -eq 0); ExitCode = $process.ExitCode })
        Assert-Condition ($process.ExitCode -eq 0) "The uninstaller launcher failed ($($process.ExitCode))."
    }
    finally { $process.Dispose() }
    # NSIS runs a temporary copy, so its launcher exit is not completion.
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not (Test-UninstallCompleted) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    Assert-Condition (Test-UninstallCompleted) 'Uninstall did not finish removing its owned files and system metadata.'
    foreach ($relative in $owned) {
        Assert-Condition (-not [IO.File]::Exists((Join-Path $installation $relative))) "Uninstall retained an owned file: $relative"
    }
    Assert-PreservedData
    $results.Add(@{ Step = 'uninstall-preserves-unowned-and-settings'; Passed = $true })
}
catch { $failure = $_; $results.Add(@{ Step = 'failure'; Passed = $false; Error = $_.Exception.ToString() }) }
finally {
    [Environment]::SetEnvironmentVariable('UDT_APPDATA_OVERRIDE', $previousOverride, 'Process')
    try { Restore-UninstallBackup $registryBackup }
    catch { $recoveryFailures.Add($_.Exception.ToString()) }
    foreach ($shortcut in $shortcutBackup) {
        try {
            Assert-NoLinks $shortcut.Path
            if ($null -eq $shortcut.Bytes) { [IO.File]::Delete($shortcut.Path) }
            else {
                $parent = [IO.Path]::GetDirectoryName($shortcut.Path)
                $null = [IO.Directory]::CreateDirectory($parent)
                [IO.File]::WriteAllBytes($shortcut.Path, $shortcut.Bytes)
            }
            if (-not $shortcut.ParentExisted) {
                $parent = [IO.Path]::GetDirectoryName($shortcut.Path)
                if ([IO.Directory]::Exists($parent) -and @([IO.Directory]::EnumerateFileSystemEntries($parent)).Count -eq 0) {
                    [IO.Directory]::Delete($parent, $false)
                }
            }
        }
        catch { $recoveryFailures.Add($_.Exception.ToString()) }
    }
    $record = @{ CreatedAt = [DateTime]::UtcNow.ToString('o'); Packages = @($primary, $compatibility);
        TemporaryRoot = $temporaryRoot; Steps = $results.ToArray(); RecoveryFailures = $recoveryFailures.ToArray();
        Complete = ($null -eq $failure -and $recoveryFailures.Count -eq 0) }
    [IO.File]::WriteAllText((Join-Path $report 'verification.json'), ($record | ConvertTo-Json -Depth 20), $utf8)
}
if ($recoveryFailures.Count -gt 0) { throw "System metadata recovery failed. See $report/system-backup.clixml and verification.json. $($recoveryFailures -join '; ')" }
if ($null -ne $failure) { throw $failure }
Write-Output "Dual-installer verification passed. Report: $report/verification.json. Isolated files retained at $temporaryRoot"
