using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using UniversalDeviceToolkit.Lib.Extensions;

namespace UniversalDeviceToolkit.Lib.Features.CursorPointer;

/// <summary>
/// Windows pointer speed / button swap and UDT cursor theme application with
/// light-dark auto switching. Absorbed from the retired custom-mouse plugin:
/// registry/SPI engine, scheme backup & restore, and the INF-based classic
/// cursor pack install are preserved verbatim; persistence moved to
/// <see cref="CursorPointerSettings"/> and logging to SharedLog.
/// </summary>
public sealed class CursorPointerService : IDisposable
{
    internal const string WindowsDefaultCursorSchemeName = "Windows Aero";

    private enum CursorTheme
    {
        Light,
        Dark
    }

    private const uint SpiSetMouseButtonSwap = 0x0021;
    private const uint SpiSetMouseSpeed = 0x0071;
    private const uint SpiSetCursors = 0x0057;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;

    private const string CursorRegistryPath = @"Control Panel\Cursors";
    private const string CursorSchemesRegistryPath = @"Control Panel\Cursors\Schemes";
    private const string CursorThemeResourceDirectoryName = "W11-CC-V2.2-HDPI";

    // Historic names written by the retired plugin so a re-install does not
    // accumulate duplicate schemes in HKCU\Control Panel\Cursors\Schemes.
    private static readonly string[] UdtCursorSchemeNames =
    [
        "UDT Custom Mouse Light",
        "UDT Custom Mouse Dark"
    ];

    private static readonly (string Key, string FileName)[] CursorSchemeOrder =
    [
        ("Arrow", "Pointer.cur"),
        ("Help", "Help.cur"),
        ("AppStarting", "Working.ani"),
        ("Wait", "Busy.ani"),
        ("crosshair", "Precision.cur"),
        ("IBeam", "Beam.cur"),
        ("NWPen", "Handwriting.cur"),
        ("No", "Unavailable.cur"),
        ("SizeNS", "Vert.cur"),
        ("SizeWE", "Horz.cur"),
        ("SizeNWSE", "Dgn1.cur"),
        ("SizeNESW", "Dgn2.cur"),
        ("SizeAll", "Move.cur"),
        ("UpArrow", "Alternate.cur"),
        ("Hand", "Link.cur"),
        ("Person", "Person.cur"),
        ("Pin", "Pin.cur")
    ];

    private static readonly string[] AdditionalCursorKeys = ["precisionhair"];

    private readonly CursorPointerSettings _settings = new();
    private readonly SystemThemeWatcher _themeWatcher = new();
    private readonly object _startGate = new();

    public static CursorThemeMode SanitizeCursorThemeMode(int raw) =>
        Enum.IsDefined(typeof(CursorThemeMode), raw) ? (CursorThemeMode)raw : CursorThemeMode.Auto;

    /// <summary>Clamps raw pointer speed to the valid SPI_SETMOUSESPEED range [1, 20].</summary>
    public static int SanitizeWindowsPointerSpeed(int raw) => Math.Clamp(raw, 1, 20);

    /// <summary>Bridges the persisted store into normalized runtime values.</summary>
    private (int Speed, bool Swap, CursorThemeMode Mode, bool AutoStyle, string LastTheme) ReadStateCore()
    {
        var store = _settings.Store;
        return (
            SanitizeWindowsPointerSpeed(store.WindowsPointerSpeed),
            store.SwapButtons,
            SanitizeCursorThemeMode(store.CursorThemeMode),
            store.AutoThemeCursorStyle,
            store.LastAppliedTheme ?? string.Empty);
    }

    public CursorPointerState GetState()
    {
        var (_, swap, mode, autoStyle, lastTheme) = ReadStateCore();
        return new CursorPointerState(SanitizeWindowsPointerSpeed(_settings.Store.WindowsPointerSpeed), swap, mode, autoStyle, lastTheme);
    }

    public async Task<bool> ApplyWindowsAsync(int speed, bool swapButtons)
    {
        if (!SetWindowsPointerSpeed(speed))
            return false;

        if (!SetSwapButtons(swapButtons))
            return false;

        await SaveAsync().ConfigureAwait(false);
        return true;
    }

    public bool SetWindowsPointerSpeed(int speed)
    {
        if (speed is < 1 or > 20)
            return false;

        if (!SystemParametersInfo(SpiSetMouseSpeed, 0, new IntPtr(speed), SpifUpdateIniFile | SpifSendChange))
            return false;

        _settings.Store.WindowsPointerSpeed = speed;
        return true;
    }

    public bool SetSwapButtons(bool swapButtons)
    {
        if (!SystemParametersInfo(SpiSetMouseButtonSwap, swapButtons ? 1u : 0u, IntPtr.Zero, SpifUpdateIniFile | SpifSendChange))
            return false;

        _settings.Store.SwapButtons = swapButtons;
        return true;
    }

    /// <summary>
    /// Boot-time hook (was the retired plugin's IAppStartupPlugin.OnAppStarted):
    /// when auto theme following is active, start the watcher and apply once if
    /// the system theme differs from the last applied one.
    /// </summary>
    public void StartRuntime()
    {
        lock (_startGate)
        {
            try
            {
                var (_, _, mode, _, _) = ReadStateCore();
                if (mode != CursorThemeMode.Auto)
                    return;

                StartThemeWatcher();
                if (!IsCurrentThemeAlreadyApplied())
                {
                    _ = Task.Run(async () =>
                    {
                        try { await ApplyCursorStyleForCurrentThemeAsync(_themeWatcher.GetCancellationToken()).ConfigureAwait(false); }
                        catch (OperationCanceledException) { /* expected during shutdown */ }
                        catch (Exception ex) { LogFailure("runtime start apply", ex); }
                    });
                }
            }
            catch (Exception ex)
            {
                LogFailure("StartRuntime", ex);
            }
        }
    }

    public void Dispose() => _themeWatcher.Dispose();

    public async Task<bool> SetCursorThemeModeAsync(CursorThemeMode mode)
    {
        if (!Enum.IsDefined(typeof(CursorThemeMode), mode))
            return false;

        if (mode == CursorThemeMode.WindowsDefault)
            return await RestoreWindowsDefaultCursorThemeAsync().ConfigureAwait(false);

        var (previousMode, previousAuto, previousLastTheme) = SnapshotThemeSettings();

        _settings.Store.CursorThemeMode = (int)mode;
        _settings.Store.AutoThemeCursorStyle = mode == CursorThemeMode.Auto;

        bool applied;
        if (mode == CursorThemeMode.Auto)
        {
            StartThemeWatcher();
            applied = await ApplyCursorStyleForCurrentThemeAsync().ConfigureAwait(false);
        }
        else
        {
            StopThemeWatcher();
            applied = await ApplySpecificCursorThemeAsync(mode == CursorThemeMode.Light ? CursorTheme.Light : CursorTheme.Dark).ConfigureAwait(false);
        }

        if (applied)
            return true;

        RollbackThemeSettings(previousMode, previousAuto, previousLastTheme);
        return false;
    }

    public async Task<bool> RestoreWindowsDefaultCursorThemeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var restored = RestoreCursorScheme(WindowsDefaultCursorSchemeName);
            if (!restored)
                return false;

            _settings.Store.AutoThemeCursorStyle = false;
            _settings.Store.CursorThemeMode = (int)CursorThemeMode.WindowsDefault;
            _settings.Store.LastAppliedTheme = string.Empty;
            _themeWatcher.NotifyThemeApplied(string.Empty);
            StopThemeWatcher();
            await SaveAsync().ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailure("restore Windows default cursor theme", ex);
            return false;
        }
    }

    public void ReloadSettingsFromSystem()
    {
        try
        {
            _settings.Store.WindowsPointerSpeed = ReadCurrentWindowsPointerSpeed();
            _settings.Store.SwapButtons = ReadCurrentSwapButtons();

            var preserveAuto = SanitizeCursorThemeMode(_settings.Store.CursorThemeMode) == CursorThemeMode.Auto
                               || _settings.Store.AutoThemeCursorStyle;
            var detectedMode = DetectCurrentCursorThemeMode();

            if (preserveAuto && detectedMode is CursorThemeMode.Light or CursorThemeMode.Dark)
            {
                _settings.Store.CursorThemeMode = (int)CursorThemeMode.Auto;
                _settings.Store.AutoThemeCursorStyle = true;
                _settings.Store.LastAppliedTheme = detectedMode == CursorThemeMode.Light ? "light" : "dark";
                return;
            }

            _settings.Store.CursorThemeMode = (int)detectedMode;
            _settings.Store.AutoThemeCursorStyle = detectedMode == CursorThemeMode.Auto;
            _settings.Store.LastAppliedTheme = detectedMode switch
            {
                CursorThemeMode.Light => "light",
                CursorThemeMode.Dark => "dark",
                _ => string.Empty
            };
        }
        catch (Exception ex)
        {
            LogFailure("reload system settings", ex);
        }
    }

    /// <summary>Re-reads system values like the former syncFromWindows bridge call.</summary>
    public async Task SyncFromWindowsAsync()
    {
        ReloadSettingsFromSystem();
        await SaveAsync().ConfigureAwait(false);
    }

    private async Task<bool> ApplySpecificCursorThemeAsync(CursorTheme theme, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await TryApplyCursorThemeWithInfAsync(theme, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplyCursorThemeFromResources(theme);
            }

            _settings.Store.LastAppliedTheme = theme == CursorTheme.Light ? "light" : "dark";
            _themeWatcher.NotifyThemeApplied(_settings.Store.LastAppliedTheme);
            await SaveAsync().ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailure($"apply cursor theme '{theme}'", ex);
            return false;
        }
    }

    public async Task<bool> ApplyCursorStyleForCurrentThemeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var theme = SystemThemeWatcher.IsSystemLightTheme() ? CursorTheme.Light : CursorTheme.Dark;

            if (!await TryApplyCursorThemeWithInfAsync(theme, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplyCursorThemeFromResources(theme);
            }

            _settings.Store.LastAppliedTheme = theme == CursorTheme.Light ? "light" : "dark";
            _themeWatcher.NotifyThemeApplied(_settings.Store.LastAppliedTheme);
            await SaveAsync().ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailure("apply current cursor theme", ex);
            return false;
        }
    }

    private Task SaveAsync()
    {
        try
        {
            return _settings.SynchronizeStoreAsync();
        }
        catch (Exception ex)
        {
            // Never let a persistence hiccup surface as a failed UI action after
            // the registry change already succeeded.
            LogFailure("save settings", ex);
            return Task.CompletedTask;
        }
    }

    private (CursorThemeMode Mode, bool AutoStyle, string LastTheme) SnapshotThemeSettings() =>
        (SanitizeCursorThemeMode(_settings.Store.CursorThemeMode),
         _settings.Store.AutoThemeCursorStyle,
         _settings.Store.LastAppliedTheme ?? string.Empty);

    private void RollbackThemeSettings(CursorThemeMode mode, bool autoStyle, string lastTheme)
    {
        _settings.Store.CursorThemeMode = (int)mode;
        _settings.Store.AutoThemeCursorStyle = autoStyle;
        _settings.Store.LastAppliedTheme = lastTheme;

        if (autoStyle)
            StartThemeWatcher();
        else
            StopThemeWatcher();
    }

    private void StartThemeWatcher()
    {
        _themeWatcher.ThemeChanged -= OnThemeChangedAsync;
        _themeWatcher.ThemeChanged += OnThemeChangedAsync;
        _themeWatcher.Start(_settings.Store.LastAppliedTheme);
    }

    private void StopThemeWatcher()
    {
        _themeWatcher.Stop();
        _themeWatcher.ThemeChanged -= OnThemeChangedAsync;
    }

    private async Task OnThemeChangedAsync(string newTheme)
    {
        _ = newTheme;
        if (SanitizeCursorThemeMode(_settings.Store.CursorThemeMode) != CursorThemeMode.Auto)
            return;

        await ApplyCursorStyleForCurrentThemeAsync(_themeWatcher.GetCancellationToken()).ConfigureAwait(false);
    }

    private bool IsCurrentThemeAlreadyApplied()
    {
        var currentTheme = SystemThemeWatcher.IsSystemLightTheme() ? "light" : "dark";
        return string.Equals(_settings.Store.LastAppliedTheme, currentTheme, StringComparison.OrdinalIgnoreCase);
    }

    private CursorThemeMode DetectCurrentCursorThemeMode()
    {
        var currentSchemeName = ReadCurrentCursorSchemeName();
        if (string.Equals(currentSchemeName, WindowsDefaultCursorSchemeName, StringComparison.OrdinalIgnoreCase))
            return CursorThemeMode.WindowsDefault;

        var arrowPath = ReadCurrentCursorValue("Arrow");
        if (!string.IsNullOrWhiteSpace(arrowPath))
        {
            if (arrowPath.IndexOf(@"\Light\", StringComparison.OrdinalIgnoreCase) >= 0)
                return CursorThemeMode.Light;

            if (arrowPath.IndexOf(@"\Dark\", StringComparison.OrdinalIgnoreCase) >= 0)
                return CursorThemeMode.Dark;
        }

        return _settings.Store.AutoThemeCursorStyle ? CursorThemeMode.Auto : GetExplicitCursorThemeMode();
    }

    private CursorThemeMode GetExplicitCursorThemeMode()
    {
        var last = _settings.Store.LastAppliedTheme ?? string.Empty;
        if (string.Equals(last, "light", StringComparison.OrdinalIgnoreCase))
            return CursorThemeMode.Light;

        if (string.Equals(last, "dark", StringComparison.OrdinalIgnoreCase))
            return CursorThemeMode.Dark;

        return SystemThemeWatcher.IsSystemLightTheme() ? CursorThemeMode.Light : CursorThemeMode.Dark;
    }

    internal static string? ReadNamedCursorScheme(string schemeName)
    {
        if (string.IsNullOrWhiteSpace(schemeName))
            return null;

        using var userSchemes = Registry.CurrentUser.OpenSubKey(CursorSchemesRegistryPath, false);
        var userValue = Convert.ToString(userSchemes?.GetValue(schemeName));
        if (!string.IsNullOrWhiteSpace(userValue))
            return userValue;

        using var machineSchemes = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cursors\Schemes", false);
        var machineValue = Convert.ToString(machineSchemes?.GetValue(schemeName));
        return string.IsNullOrWhiteSpace(machineValue) ? null : machineValue;
    }

    private async Task<bool> TryApplyCursorThemeWithInfAsync(CursorTheme theme, CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            var infPath = GetInstallInfPath(theme);
            if (!File.Exists(infPath))
                return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"setupapi.dll,InstallHinfSection DefaultInstall 132 \"{infPath}\"",
                WorkingDirectory = Path.GetDirectoryName(infPath) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = Process.Start(startInfo);
            if (process == null)
                return false;

            var waitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();

            if (completedTask != waitTask)
                return false;

            await waitTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!process.HasExited || process.ExitCode != 0)
                return false;

            ApplySystemCursorRefresh();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailure("INF installation", ex);
            return false;
        }
        finally
        {
            if (process != null)
            {
                TryKillProcess(process);
                process.Dispose();
            }
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Cleanup failures are tolerated; the caller reports timeout/cancellation.
        }
    }

    private void ApplyCursorThemeFromResources(CursorTheme theme)
    {
        BackupCurrentCursorSchemeIfNeeded();

        var basePath = GetBaseCursorPath(theme);
        var animationPath = GetAnimationCursorPath(theme);
        var schemeName = theme == CursorTheme.Light ? UdtCursorSchemeNames[0] : UdtCursorSchemeNames[1];

        EnsureCursorResourcesExist(basePath, animationPath);

        using var cursorKey = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true)
                             ?? throw new InvalidOperationException("Failed to open cursor registry path.");
        using var schemesKey = Registry.CurrentUser.CreateSubKey(CursorSchemesRegistryPath, true)
                              ?? throw new InvalidOperationException("Failed to open cursor schemes registry path.");

        var schemeEntries = new List<string>(CursorSchemeOrder.Length);
        foreach (var (key, fileName) in CursorSchemeOrder)
        {
            var path = ResolveCursorFilePath(basePath, animationPath, fileName);
            cursorKey.SetValue(key, path, RegistryValueKind.ExpandString);
            schemeEntries.Add(path);
        }

        var precisionPath = Path.Combine(basePath, "Precision.cur");
        foreach (var additionalKey in AdditionalCursorKeys)
        {
            cursorKey.SetValue(additionalKey, precisionPath, RegistryValueKind.ExpandString);
        }

        cursorKey.SetValue(string.Empty, schemeName, RegistryValueKind.String);
        schemesKey.SetValue(schemeName, string.Join(",", schemeEntries), RegistryValueKind.ExpandString);

        ApplySystemCursorRefresh();
    }

    internal bool RestoreCursorScheme(string schemeName)
    {
        if (string.IsNullOrWhiteSpace(schemeName))
            return false;

        using var cursorKey = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true);
        if (cursorKey == null)
        {
            if (Shared.Logging.SharedLog.IsTraceEnabled)
                Shared.Logging.SharedLog.Trace("CursorPointer: failed to open cursor registry path.");
            return false;
        }

        var rawScheme = ReadNamedCursorScheme(schemeName);
        if (string.IsNullOrWhiteSpace(rawScheme))
            return false;

        var parts = rawScheme.Split(',');
        // Classic Windows Aero schemes have 15 entries; Person/Pin were added later.
        if (parts.Length < 15)
            return false;

        cursorKey.SetValue(string.Empty, schemeName, RegistryValueKind.String);
        for (var i = 0; i < CursorSchemeOrder.Length; i++)
        {
            var path = i < parts.Length && !string.IsNullOrWhiteSpace(parts[i])
                ? parts[i]
                : parts[0];
            cursorKey.SetValue(CursorSchemeOrder[i].Key, path, RegistryValueKind.ExpandString);
        }

        var precisionPath = GetPrecisionCursorPath(parts);
        foreach (var additionalKey in AdditionalCursorKeys)
        {
            cursorKey.SetValue(additionalKey, precisionPath, RegistryValueKind.ExpandString);
        }

        cursorKey.SetValue("Scheme Source", 1, RegistryValueKind.DWord);
        ApplySystemCursorRefresh();
        return true;
    }

    /// <summary>
    /// Restores the backed-up pre-UDT cursor scheme captured before the first
    /// UDT cursor-theme apply. Backed up through the retired plugin (flat keys)
    /// or this service since absorption.
    /// </summary>
    internal bool TryRestoreBackedUpCursorScheme()
    {
        if (!_settings.Store.CursorBackupSaved)
            return true;

        return RestoreBackedUpCursorSchemeInternal();
    }

    private bool RestoreBackedUpCursorSchemeInternal()
    {
        try
        {
            using var cursorKey = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true);
            if (cursorKey == null)
            {
                if (Shared.Logging.SharedLog.IsTraceEnabled)
                    Shared.Logging.SharedLog.Trace("CursorPointer: failed to open cursor registry path for backup restore.");
                return false;
            }

            var store = _settings.Store;
            cursorKey.SetValue(string.Empty, store.CursorBackupDefault ?? string.Empty, RegistryValueKind.String);

            foreach (var (key, _) in CursorSchemeOrder)
            {
                var backupValue = store.CursorBackup.TryGetValue(key, out var value) ? value : string.Empty;
                cursorKey.SetValue(key, backupValue, RegistryValueKind.ExpandString);
            }

            foreach (var additionalKey in AdditionalCursorKeys)
            {
                var backupValue = store.CursorBackup.TryGetValue(additionalKey, out var value) ? value : string.Empty;
                cursorKey.SetValue(additionalKey, backupValue, RegistryValueKind.ExpandString);
            }

            ApplySystemCursorRefresh();
            return true;
        }
        catch (Exception ex)
        {
            LogFailure("restore backed-up cursor scheme", ex);
            return false;
        }
    }

    private void BackupCurrentCursorSchemeIfNeeded()
    {
        var store = _settings.Store;
        if (store.CursorBackupSaved)
            return;

        using var cursorKey = Registry.CurrentUser.OpenSubKey(CursorRegistryPath, false);
        if (cursorKey == null)
            return;

        store.CursorBackupDefault = Convert.ToString(cursorKey.GetValue(string.Empty)) ?? string.Empty;
        foreach (var (key, _) in CursorSchemeOrder)
        {
            store.CursorBackup[key] = Convert.ToString(cursorKey.GetValue(key)) ?? string.Empty;
        }

        foreach (var additionalKey in AdditionalCursorKeys)
        {
            store.CursorBackup[additionalKey] = Convert.ToString(cursorKey.GetValue(additionalKey)) ?? string.Empty;
        }

        store.CursorBackupSaved = true;
        SaveAsync().Forget("persist cursor scheme backup");
    }

    private static int ReadCurrentWindowsPointerSpeed()
    {
        using var mouseKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", false);
        var raw = Convert.ToString(mouseKey?.GetValue("MouseSensitivity"));
        return int.TryParse(raw, out var speed) && speed >= 1 && speed <= 20 ? speed : 10;
    }

    private static bool ReadCurrentSwapButtons()
    {
        using var mouseKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", false);
        var raw = Convert.ToString(mouseKey?.GetValue("SwapMouseButtons"));
        return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadCurrentCursorSchemeName()
    {
        using var cursorKey = Registry.CurrentUser.OpenSubKey(CursorRegistryPath, false);
        return Convert.ToString(cursorKey?.GetValue(string.Empty)) ?? string.Empty;
    }

    private static string ReadCurrentCursorValue(string valueName)
    {
        using var cursorKey = Registry.CurrentUser.OpenSubKey(CursorRegistryPath, false);
        return Convert.ToString(cursorKey?.GetValue(valueName)) ?? string.Empty;
    }

    private string GetInstallInfPath(CursorTheme theme)
    {
        var themeName = theme == CursorTheme.Light ? "Light" : "Dark";
        return Path.Combine(
            GetResourceRoot(),
            CursorThemeResourceDirectoryName,
            themeName,
            "Regular",
            "02. classic",
            "Install.inf");
    }

    private string GetBaseCursorPath(CursorTheme theme)
    {
        var themeName = theme == CursorTheme.Light ? "Light" : "Dark";
        return Path.Combine(
            GetResourceRoot(),
            CursorThemeResourceDirectoryName,
            themeName,
            "Regular",
            "Base");
    }

    private string GetAnimationCursorPath(CursorTheme theme)
    {
        var themeName = theme == CursorTheme.Light ? "Light" : "Dark";
        var candidate = Path.Combine(
            GetResourceRoot(),
            CursorThemeResourceDirectoryName,
            themeName,
            "Regular",
            "02. classic");

        if (Directory.Exists(candidate))
            return candidate;

        return Path.Combine(
            GetResourceRoot(),
            CursorThemeResourceDirectoryName,
            "Dark",
            "Regular",
            "02. classic");
    }

    /// <summary>
    /// Primary location is CursorPointerAssets next to the host executable (set up
    /// by the Lib csproj content copy). The retired plugin's install directories are
    /// kept as fallbacks so an upgrade keeps working before/without re-download.
    /// </summary>
    internal static IEnumerable<string> EnumerateResourceRootCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "CursorPointerAssets");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "UniversalDeviceToolkit", "plugins", "custom-mouse", "Resources");
            yield return Path.Combine(localAppData, "UniversalDeviceToolkit", "plugins", "local", "custom-mouse", "Resources");
        }
    }

    private static string GetResourceRoot() =>
        EnumerateResourceRootCandidates()
            .FirstOrDefault(IsCursorResourceRoot)
            ?? EnumerateResourceRootCandidates().First();

    private static bool IsCursorResourceRoot(string resourceRoot) =>
        !string.IsNullOrWhiteSpace(resourceRoot)
        && Directory.Exists(Path.Combine(resourceRoot, CursorThemeResourceDirectoryName));

    private static string ResolveCursorFilePath(string basePath, string animationPath, string fileName) =>
        fileName.EndsWith(".ani", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(animationPath, fileName)
            : Path.Combine(basePath, fileName);

    private static void EnsureCursorResourcesExist(string basePath, string animationPath)
    {
        foreach (var (_, fileName) in CursorSchemeOrder)
        {
            var fullPath = ResolveCursorFilePath(basePath, animationPath, fileName);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Cursor resource file not found: {fullPath}");
        }
    }

    private static void ApplySystemCursorRefresh()
    {
        SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, SpifSendChange);
    }

    private static string GetPrecisionCursorPath(string[] schemeParts)
    {
        if (schemeParts.Length > 4 && !string.IsNullOrWhiteSpace(schemeParts[4]))
            return schemeParts[4];

        return schemeParts.Length > 0 ? schemeParts[0] : string.Empty;
    }

    private static void LogFailure(string operation, Exception ex)
    {
        if (Shared.Logging.SharedLog.IsTraceEnabled)
            Shared.Logging.SharedLog.Trace($"CursorPointer: {operation} failed.", ex);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
}

/// <summary>Bridge-facing snapshot of cursor &amp; pointer feature state.</summary>
public sealed record CursorPointerState(
    int PointerSpeed,
    bool SwapButtons,
    CursorThemeMode CursorThemeMode,
    bool AutoThemeCursorStyle,
    string LastAppliedTheme);
