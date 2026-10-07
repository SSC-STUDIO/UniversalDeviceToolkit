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

function Initialize-VerificationProcess {
    if ('UdtVerification.OwnedProcess' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace UdtVerification {
    public sealed class OwnedProcess : IDisposable {
        private IntPtr job;
        private Process process;
        public int ExitCode { get { return process.ExitCode; } }
        public bool HasExited { get { return process.HasExited; } }
        public int Id { get { return process.Id; } }
        public IntPtr MainWindowHandle { get { process.Refresh(); return process.MainWindowHandle; } }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimit {
            public long ProcessTime, JobTime;
            public uint Flags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimit {
            public BasicLimit Basic;
            public ulong ReadOperations, WriteOperations, OtherOperations;
            public ulong ReadBytes, WriteBytes, OtherBytes;
            public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Accounting {
            public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
            public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo {
            public int Size;
            public string Reserved, Desktop, Title;
            public uint X, Y, Width, Height, XCharacters, YCharacters, Fill, Flags;
            public ushort ShowWindow, ReservedSize;
            public IntPtr ReservedBytes, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInfo {
            public IntPtr Process, Thread;
            public uint ProcessId, ThreadId;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr job, int kind, ref ExtendedLimit info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryInformationJobObject(IntPtr job, int kind, out Accounting info, uint size, IntPtr returned);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity,
            IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory,
            ref StartupInfo startup, out ProcessInfo info);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(IntPtr job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        public static OwnedProcess Start(string path, string arguments) {
            var owner = new OwnedProcess();
            var info = new ProcessInfo();
            try {
                owner.job = CreateJobObject(IntPtr.Zero, null);
                if (owner.job == IntPtr.Zero) throw new Win32Exception();
                var limit = new ExtendedLimit();
                limit.Basic.Flags = 0x2000;
                if (!SetInformationJobObject(owner.job, 9, ref limit, (uint)Marshal.SizeOf(typeof(ExtendedLimit))))
                    throw new Win32Exception();
                var startup = new StartupInfo();
                startup.Size = Marshal.SizeOf(typeof(StartupInfo));
                var command = new StringBuilder("\"" + path + "\" " + arguments);
                // Assign before the first instruction so every child stays in this invocation's job.
                if (!CreateProcess(path, command, IntPtr.Zero, IntPtr.Zero, false, 0x08000004,
                    IntPtr.Zero, null, ref startup, out info)) throw new Win32Exception();
                if (!AssignProcessToJobObject(owner.job, info.Process)) throw new Win32Exception();
                owner.process = Process.GetProcessById((int)info.ProcessId);
                var pinnedHandle = owner.process.Handle;
                if (ResumeThread(info.Thread) == uint.MaxValue) throw new Win32Exception();
                return owner;
            }
            catch {
                if (info.Process != IntPtr.Zero) TerminateProcess(info.Process, 1);
                owner.Dispose();
                throw;
            }
            finally {
                if (info.Thread != IntPtr.Zero) CloseHandle(info.Thread);
                if (info.Process != IntPtr.Zero) CloseHandle(info.Process);
            }
        }

        private bool IsComplete() {
            Accounting info;
            if (!QueryInformationJobObject(job, 1, out info, (uint)Marshal.SizeOf(typeof(Accounting)), IntPtr.Zero))
                throw new Win32Exception();
            return info.ActiveProcesses == 0;
        }

        public bool WaitForCompletion(int milliseconds) {
            var timer = Stopwatch.StartNew();
            while (!IsComplete()) {
                if (timer.ElapsedMilliseconds >= milliseconds) return false;
                Thread.Sleep(25);
            }
            process.WaitForExit();
            return true;
        }

        public void Stop() {
            if (job == IntPtr.Zero) return;
            if (!IsComplete() && !TerminateJobObject(job, 1)) throw new Win32Exception();
            if (!WaitForCompletion(10000)) throw new TimeoutException("Owned verification processes did not stop.");
        }

        public void Dispose() {
            if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; }
            if (process != null) { process.Dispose(); process = null; }
        }
    }
}
'@
}

function Start-VerificationProcess([string]$Path, [string]$Arguments) {
    Initialize-VerificationProcess
    $ownedProcess = [UdtVerification.OwnedProcess]::Start($Path, $Arguments)
    $verificationProcesses.Add($ownedProcess)
    return $ownedProcess
}

function Stop-VerificationProcesses {
    $failures = New-Object 'System.Collections.Generic.List[string]'
    foreach ($ownedProcess in $verificationProcesses) {
        try { $ownedProcess.Stop(); $ownedProcess.Dispose() }
        catch { $failures.Add($_.Exception.ToString()) }
    }
    if ($failures.Count -gt 0) { throw ($failures -join '; ') }
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

function Restore-ShortcutBackup([object[]]$Entries) {
    $failures = New-Object 'System.Collections.Generic.List[string]'
    foreach ($shortcut in $Entries) {
        try {
            Assert-NoLinks $shortcut.Path
            if ($null -eq $shortcut.Bytes) {
                if ([IO.File]::Exists($shortcut.Path)) { [IO.File]::Delete($shortcut.Path) }
            }
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

function Invoke-Installer([hashtable]$Package, [switch]$ExpectFailure) {
    Assert-NoLinks $installation
    Assert-Condition ((Get-FileHash -LiteralPath $Package.Path -Algorithm SHA256).Hash -eq $Package.Hash) 'The installer changed after verification.'
    # NSIS requires /D= last and unquoted, including a destination with spaces.
    $process = Start-VerificationProcess $Package.Path "/S /D=$installation"
    if (-not $process.WaitForCompletion($TimeoutSeconds * 1000)) {
        $process.Stop()
        throw "Installer timed out: $($Package.Name)"
    }
    if ($ExpectFailure) { Assert-Condition ($process.ExitCode -ne 0) 'The injected registration failure was unexpectedly accepted.' }
    else { Assert-Condition ($process.ExitCode -eq 0) "Installer failed ($($process.ExitCode)): $($Package.Name)" }
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

function Start-InstalledDiagnostic([string]$Channel) {
    $names = @('UDT_DIAGNOSTIC_MODE', 'UDT_UI_INSPECTION_PHASE', 'UDT_UI_INSPECTION_SECONDS', 'UDT_APPDATA_OVERRIDE')
    $previous = @{}
    foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    try {
        $env:UDT_DIAGNOSTIC_MODE = '1'
        $env:UDT_UI_INSPECTION_PHASE = 'startup'
        $env:UDT_UI_INSPECTION_SECONDS = '60'
        $env:UDT_APPDATA_OVERRIDE = Join-Path $temporaryRoot ($Channel + ' diagnostic appdata')
        $arguments = if ($Channel -eq 'webview2') { '--diagnose-ui' } else { '--no-hardware --safe-start --disable-update-checker' }
        $diagnostic = Start-VerificationProcess (Join-Path $installation 'UniversalDeviceToolkit.exe') $arguments
    }
    finally {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not $diagnostic.HasExited -and $diagnostic.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
    }
    Assert-Condition (-not $diagnostic.HasExited -and $diagnostic.MainWindowHandle -ne [IntPtr]::Zero) "The installed $Channel diagnostic did not open a window."
    $results.Add(@{ Step = "$Channel-running-installed-window"; Passed = $true; ProcessId = $diagnostic.Id })
    return $diagnostic
}

function Invoke-VerifiedUninstall([string]$Channel) {
    Assert-Installation $Channel
    $diagnostic = Start-InstalledDiagnostic $Channel
    $script:owned = @([IO.File]::ReadAllText((Join-Path $installation 'resources/install-files.json')) | ConvertFrom-Json)
    $script:uninstaller = Join-Path $installation 'Uninstall.exe'
    $process = Start-VerificationProcess $uninstaller '/S'
    if (-not $process.WaitForCompletion($TimeoutSeconds * 1000)) {
        $process.Stop()
        throw "Uninstaller timed out: $Channel"
    }
    $results.Add(@{ Step = "$Channel-uninstaller-launcher"; Passed = ($process.ExitCode -eq 0); ExitCode = $process.ExitCode })
    Assert-Condition ($process.ExitCode -eq 0) "The $Channel uninstaller launcher failed ($($process.ExitCode))."
    Assert-Condition (Test-UninstallCompleted) 'Uninstall did not finish removing its owned files and system metadata.'
    Assert-Condition ($diagnostic.WaitForCompletion(10000)) 'Uninstall left processes from the installed diagnostic running.'
    Assert-PreservedData
    $results.Add(@{ Step = "$Channel-uninstall-preserves-unowned-and-settings"; Passed = $true })
}

function Get-RegistryFingerprint([hashtable]$Snapshot) {
    if ($null -eq $Snapshot) { return 'absent' }
    $records = @()
    foreach ($name in ($Snapshot.Values.Keys | Sort-Object)) {
        $value = $Snapshot.Values[$name]
        $records += @($name, $value.Kind, $value.Value) | ConvertTo-Json -Compress
    }
    foreach ($name in ($Snapshot.Children.Keys | Sort-Object)) {
        $records += @($name, (Get-RegistryFingerprint $Snapshot.Children[$name])) | ConvertTo-Json -Compress
    }
    return ($records | ConvertTo-Json -Compress)
}

function Get-InstallationSnapshot {
    $files = @{}
    $manifestPath = Join-Path $installation 'resources/install-files.json'
    foreach ($relative in @([IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json)) {
        $files[$relative] = (Get-FileHash -LiteralPath (Join-Path $installation $relative) -Algorithm SHA256).Hash
    }
    $shortcuts = @{}
    foreach ($shortcut in $shortcutBackup) { $shortcuts[$shortcut.Path] = [Convert]::ToBase64String([IO.File]::ReadAllBytes($shortcut.Path)) }
    return @{ Files = $files; Shortcuts = $shortcuts; Registry = (Get-UninstallBackup);
        AllFiles = @([IO.Directory]::EnumerateFiles($installation, '*', [IO.SearchOption]::AllDirectories) | Sort-Object) }
}

function Assert-InstallationSnapshot([hashtable]$Snapshot) {
    $currentFiles = @([IO.Directory]::EnumerateFiles($installation, '*', [IO.SearchOption]::AllDirectories) | Sort-Object)
    Assert-Condition (($currentFiles -join "`n") -eq ($Snapshot.AllFiles -join "`n")) 'Rollback left a different set of installation files.'
    foreach ($relative in $Snapshot.Files.Keys) {
        $path = Join-Path $installation $relative
        Assert-Condition ([IO.File]::Exists($path)) "Rollback lost an installed file: $relative"
        Assert-Condition ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $Snapshot.Files[$relative]) "Rollback changed an installed file: $relative"
    }
    foreach ($path in $Snapshot.Shortcuts.Keys) {
        Assert-Condition ([IO.File]::Exists($path)) "Rollback lost a shortcut: $path"
        Assert-Condition ([Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -eq $Snapshot.Shortcuts[$path]) 'Rollback changed an existing shortcut.'
    }
    foreach ($entry in $Snapshot.Registry) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, [Microsoft.Win32.RegistryView]$entry.View)
        try {
            $key = $root.OpenSubKey("$uninstallRoot\$($entry.Name)")
            try { $current = if ($null -eq $key) { $null } else { Get-RegistrySnapshot $key } }
            finally { if ($null -ne $key) { $key.Dispose() } }
            Assert-Condition ((Get-RegistryFingerprint $current) -eq (Get-RegistryFingerprint $entry.Snapshot)) 'Rollback changed uninstall registration.'
        }
        finally { $root.Dispose() }
    }
    Assert-PreservedData
}

function Test-RegistrationRollback {
    $snapshot = Get-InstallationSnapshot
    # A read-sharing lock allows registration to snapshot the old shortcut,
    # then makes native CreateShortCut fail after the new payload was copied.
    $shortcutLock = [IO.File]::Open($shortcutBackup[0].Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try { Invoke-Installer $compatibility -ExpectFailure }
    finally { $shortcutLock.Dispose() }
    Assert-InstallationSnapshot $snapshot
    Assert-Installation 'webview2'
    $results.Add(@{ Step = 'registration-failure-restores-installation-and-metadata'; Passed = $true })
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
$verificationProcesses = New-Object 'System.Collections.Generic.List[object]'
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
    Test-RegistrationRollback
    Invoke-Installer $compatibility
    Assert-Installation 'electron-compatibility'
    Assert-PreservedData
    $results.Add(@{ Step = 'webview2-to-electron'; Passed = $true })
    Invoke-Installer $compatibility
    Assert-Installation 'electron-compatibility'
    Assert-PreservedData
    $results.Add(@{ Step = 'same-channel-electron-reinstall'; Passed = $true })
    Invoke-VerifiedUninstall 'electron-compatibility'
    Invoke-Installer $compatibility
    Assert-Installation 'electron-compatibility'
    Assert-PreservedData
    $results.Add(@{ Step = 'electron-reinstall-after-independent-uninstall'; Passed = $true })
    Invoke-Installer $primary
    Assert-Installation 'webview2'
    Assert-PreservedData
    $results.Add(@{ Step = 'electron-to-webview2'; Passed = $true })
    Invoke-VerifiedUninstall 'webview2'
}
catch { $failure = $_; $results.Add(@{ Step = 'failure'; Passed = $false; Error = $_.Exception.ToString() }) }
finally {
    [Environment]::SetEnvironmentVariable('UDT_APPDATA_OVERRIDE', $previousOverride, 'Process')
    $canRestoreMetadata = $true
    try { Stop-VerificationProcesses }
    catch {
        $canRestoreMetadata = $false
        $recoveryFailures.Add('System metadata was not restored because owned test processes could still be running. ' + $_.Exception.ToString())
    }
    if ($canRestoreMetadata) {
        try { Restore-UninstallBackup $registryBackup }
        catch { $recoveryFailures.Add($_.Exception.ToString()) }
        try { Restore-ShortcutBackup $shortcutBackup }
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
