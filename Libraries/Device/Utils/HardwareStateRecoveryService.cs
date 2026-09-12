using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Implementation strategy for <see cref="HardwareStateRecoveryService"/>.
/// Static methods talk to the live <see cref="IoCContainer"/> (production
/// usage). Tests construct an instance with the explicit delegate overload to
/// inject deterministic behavior without spinning up the IoC container.
/// </summary>
public sealed class HardwareStateRecoveryImplementation
{
    public Func<Type, object?> TryResolve { get; }
    public Action<string> Console { get; }
    public Action<string>? Trace { get; }

    public HardwareStateRecoveryImplementation(
        Func<Type, object?> tryResolve,
        Action<string> console,
        Action<string>? trace = null)
    {
        TryResolve = tryResolve ?? throw new ArgumentNullException(nameof(tryResolve));
        Console = console ?? throw new ArgumentNullException(nameof(console));
        Trace = trace;
    }

    public static HardwareStateRecoveryImplementation Default() =>
        new(t => IoCContainer.TryResolve<object>(), msg => global::System.Console.WriteLine(msg));
}

/// <summary>
/// Restores hardware / network settings to factory defaults and quarantines
/// corrupted configuration files. Designed for <c>--reset-hardware-state</c>
/// and <c>--reset-network-state</c> start-up switches, plus an automated
/// fallback when <see cref="StartupHealthGuard.ShouldEnterSafeMode"/> is true.
///
/// All reset methods return a human-readable report and a success flag. Any
/// missing IoC-registered component is treated as "skipped (component not
/// initialized)" rather than a hard failure — the contract is best-effort
/// recovery, not a verifiable invariant.
/// </summary>
public sealed class HardwareStateRecoveryService
{
    private readonly HardwareStateRecoveryImplementation _impl;

    public HardwareStateRecoveryService()
        : this(HardwareStateRecoveryImplementation.Default())
    {
    }

    public HardwareStateRecoveryService(HardwareStateRecoveryImplementation implementation)
    {
        _impl = implementation ?? throw new ArgumentNullException(nameof(implementation));
    }

    private const string SectionSeparator = "----------------------------------------";

    /// <summary>
    /// Default minimum processor state (%) written when
    /// <paramref name="restoreProcessorMinState"/> is true. Matches a
    /// conservative Balanced-plan style floor (not 0%, which can break boost).
    /// </summary>
    public const uint DefaultProcessorMinStatePercent = 5;

    /// <summary>
    /// Processor power management subgroup GUID (GUID_PROCESSOR_SETTINGS_SUBGROUP).
    /// </summary>
    public static readonly Guid ProcessorSettingsSubgroupGuid =
        new("54533251-82be-4824-96c1-47b60b740d00");

    /// <summary>
    /// Minimum processor state setting GUID (PROCTHROTTLEMIN).
    /// </summary>
    public static readonly Guid ProcessorThrottleMinGuid =
        new("893dee8e-2bef-41e0-89c6-b55d0929964c");

    /// <summary>
    /// Resets every hardware-related feature the app owns back to its factory
    /// state. Never throws; collects results in <paramref name="report"/>.
    /// Always attempts God Mode → Balance when the power mode feature is available.
    /// </summary>
    public bool TryResetHardware(out string report) =>
        TryResetHardware(out report, restoreProcessorMinState: false);

    /// <summary>
    /// Resets hardware state. When <paramref name="restoreProcessorMinState"/> is
    /// true, also writes minimum processor state on the <em>currently active</em>
    /// Windows power plan (AC + DC). That path intentionally edits the user's
    /// active plan — leave false unless the operator explicitly requested
    /// <c>--restore-processor-min-state</c>.
    /// </summary>
    public bool TryResetHardware(out string report, bool restoreProcessorMinState)
    {
        var success = true;
        var sb = new StringBuilder();
        sb.AppendLine("Hardware state reset report");
        sb.AppendLine(SectionSeparator);

        // P1: exit God Mode first so subsequent resets run under Balance.
        success &= TryExitGodModeToBalance(sb);
        sb.AppendLine();

        success &= TryResetRgbKeyboard(sb);
        sb.AppendLine();

        success &= TryStopSpectrum(sb);
        sb.AppendLine();

        success &= TryDisableLenovoLighting(sb);
        sb.AppendLine();

        success &= TryDisableExperimentalGpu(sb);
        sb.AppendLine();

        success &= TryClearPowerModeCustomizations(sb);
        sb.AppendLine();

        success &= TryResetFanCurves(sb);
        sb.AppendLine();

        success &= TryResetBalanceMode(sb);
        sb.AppendLine();

        if (restoreProcessorMinState)
        {
            success &= TryRestoreProcessorMinState(sb, DefaultProcessorMinStatePercent);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine(
                "[processor-min-state] skipped (optional; pass restoreProcessorMinState=true / " +
                "--restore-processor-min-state to edit the active Windows power plan).");
            sb.AppendLine();
        }

        sb.AppendLine(SectionSeparator);
        sb.AppendLine(success ? "Result: OK" : "Result: PARTIAL (see skipped entries above)");
        report = sb.ToString();
        TryWriteConsole(report);
        return success;
    }

    /// <summary>
    /// Removes the args.txt passthrough proxy switches, stops running network acceleration,
    /// then restores system proxy / UDT hosts / PAC from the last network snapshot when present
    /// (<see cref="INetworkStateRecoveryService"/>). Idempotent with an empty
    /// or missing snapshot.
    /// </summary>
    public bool TryResetNetwork(out string report)
    {
        var success = true;
        var sb = new StringBuilder();
        sb.AppendLine("Network state reset report");
        sb.AppendLine(SectionSeparator);

        try
        {
            var networkService = _impl.TryResolve(typeof(INetworkAccelerationService)) as INetworkAccelerationService;
            if (networkService is { IsRunning: true })
            {
                networkService.StopAsync().GetAwaiter().GetResult();
                sb.AppendLine("network-acceleration: stopped running service.");
            }
            else
            {
                sb.AppendLine("network-acceleration: no running service to stop.");
            }
        }
        catch (Exception ex)
        {
            success = false;
            sb.AppendLine($"network-acceleration: failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: network acceleration stop failed.", ex);
        }

        try
        {
            var argsPath = Path.Combine(Folders.AppData, "args.txt");
            if (!File.Exists(argsPath))
            {
                sb.AppendLine("args.txt: skipped (file not present).");
            }
            else
            {
                var lines = File.ReadAllLines(argsPath);
                var filtered = StripProxyArgs(lines);
                var removed = lines.Length - filtered.Length;
                if (removed == 0 && lines.Length == 0)
                {
                    sb.AppendLine("args.txt: skipped (file empty).");
                }
                else
                {
                    File.WriteAllLines(argsPath, filtered);
                    sb.AppendLine(removed > 0
                        ? $"args.txt: {removed} proxy-related entries removed."
                        : "args.txt: no proxy entries to remove.");
                }
            }
        }
        catch (Exception ex)
        {
            success = false;
            sb.AppendLine($"args.txt: failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace($"HardwareStateRecoveryService: args.txt reset failed.", ex);
        }

        sb.AppendLine();
        try
        {
            var recovery = _impl.TryResolve(typeof(INetworkStateRecoveryService)) as INetworkStateRecoveryService
                           ?? new NetworkStateRecoveryService();
            var ok = recovery.TryRestoreFromSnapshot(out var recoveryReport);
            success &= ok;
            sb.AppendLine(recoveryReport.TrimEnd());
        }
        catch (Exception ex)
        {
            success = false;
            sb.AppendLine($"snapshot restore: failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: snapshot restore failed.", ex);
        }

        sb.AppendLine(SectionSeparator);
        sb.AppendLine(success ? "Result: OK" : "Result: PARTIAL (see failures above)");
        report = sb.ToString();
        TryWriteConsole(report);
        return success;
    }

    /// <summary>
    /// Moves <c>settings.json</c> from <see cref="Folders.AppData"/> to
    /// <c>settings.json.bak.&lt;timestamp&gt;</c> and writes the destination
    /// path back into <paramref name="report"/>. A missing source file is
    /// treated as success (nothing to back up).
    /// </summary>
    public bool TryBackupCorruptedConfig(out string report)
    {
        return TryBackupFile("settings.json", out report);
    }

    /// <summary>
    /// Generic backup helper used by <see cref="TryBackupCorruptedConfig"/>.
    /// Public so a future caller (e.g. a script-driven recovery) can target
    /// other settings files without duplicating IO error handling.
    /// </summary>
    public bool TryBackupFile(string filename, out string report)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            report = $"No filename supplied.";
            return false;
        }

        var sourcePath = Path.Combine(Folders.AppData, filename);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = BuildBackupPath(Folders.AppData, filename, timestamp);

        var sb = new StringBuilder();
        sb.AppendLine($"Backup report for '{filename}'");

        try
        {
            if (!File.Exists(sourcePath))
            {
                sb.AppendLine($"  source: not present (nothing to back up).");
                sb.AppendLine($"  backup: {backupPath}");
                report = sb.ToString();
                TryWriteConsole(report);
                return true;
            }

            File.Move(sourcePath, backupPath, overwrite: false);
            sb.AppendLine($"  source: {sourcePath}");
            sb.AppendLine($"  backup: {backupPath}");
            sb.AppendLine("  status: OK");
            report = sb.ToString();
            TryWriteConsole(report);
            return true;
        }
        catch (IOException ex) when (File.Exists(backupPath))
        {
            sb.AppendLine($"  backup: {backupPath}");
            sb.AppendLine($"  status: FAILURE - destination already exists ({ex.Message}).");
            report = sb.ToString();
            TryWriteConsole(report);
            return false;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  source: {sourcePath}");
            sb.AppendLine($"  backup: {backupPath}");
            sb.AppendLine($"  status: FAILURE - {ex.GetType().Name}: {ex.Message}");
            report = sb.ToString();
            TryWriteConsole(report);
            TryTrace($"HardwareStateRecoveryService: backup '{filename}' failed.", ex);
            return false;
        }
    }

    private static string BuildBackupPath(string folder, string filename, string timestamp)
    {
        var ext = Path.GetExtension(filename);
        var stem = Path.GetFileName(filename);
        return Path.Combine(folder, $"{stem}.bak.{timestamp}");
    }

    /// <summary>
    /// Switches Legion power mode from God Mode to Balance when the feature is
    /// available and currently in God Mode. Best-effort; missing components skip.
    /// </summary>
    private bool TryExitGodModeToBalance(StringBuilder sb)
    {
        sb.Append("[god-mode-to-balance] ");
        try
        {
            if (_impl.TryResolve(typeof(PowerModeFeature)) is not PowerModeFeature powerMode)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            var state = powerMode.GetStateAsync().GetAwaiter().GetResult();
            if (state != PowerModeState.GodMode)
            {
                sb.AppendLine($"already not God Mode (current={state}).");
                return true;
            }

            powerMode.SetStateAsync(PowerModeState.Balance).GetAwaiter().GetResult();
            sb.AppendLine("exited God Mode → Balance.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: God Mode → Balance failed.", ex);
            return false;
        }
    }

    /// <summary>
    /// Writes processor minimum state (%) on the active Windows power plan for
    /// both AC and DC. Explicitly mutates the user's active plan.
    /// </summary>
    private bool TryRestoreProcessorMinState(StringBuilder sb, uint percent)
    {
        sb.Append("[processor-min-state] ");
        try
        {
            percent = Math.Min(100u, percent);

            unsafe
            {
                if (PInvoke.PowerGetActiveScheme(null, out var schemePtr) != WIN32_ERROR.ERROR_SUCCESS
                    || schemePtr is null)
                {
                    sb.AppendLine("failure (PowerGetActiveScheme).");
                    return false;
                }

                try
                {
                    var scheme = *schemePtr;
                    // PowerWrite* returns WIN32 status as uint (0 = ERROR_SUCCESS).
                    var ac = PInvoke.PowerWriteACValueIndex(
                        NullSafeHandle.Null,
                        scheme,
                        ProcessorSettingsSubgroupGuid,
                        ProcessorThrottleMinGuid,
                        percent);
                    var dc = PInvoke.PowerWriteDCValueIndex(
                        NullSafeHandle.Null,
                        scheme,
                        ProcessorSettingsSubgroupGuid,
                        ProcessorThrottleMinGuid,
                        percent);

                    // PowerWrite* APIs return WIN32 status as uint (0 = ERROR_SUCCESS).
                    if ((WIN32_ERROR)ac != WIN32_ERROR.ERROR_SUCCESS || (WIN32_ERROR)dc != WIN32_ERROR.ERROR_SUCCESS)
                    {
                        sb.AppendLine($"failure (PowerWriteAC/DC ValueIndex ac={ac}, dc={dc}).");
                        return false;
                    }

                    // Re-activate so the new index takes effect immediately.
                    var activate = PInvoke.PowerSetActiveScheme(null, scheme);
                    if (activate != WIN32_ERROR.ERROR_SUCCESS)
                    {
                        sb.AppendLine(
                            $"wrote min={percent}% but PowerSetActiveScheme failed ({activate}); " +
                            "values may apply on next plan switch.");
                        return false;
                    }

                    sb.AppendLine(
                        $"set minimum processor state to {percent}% on active plan {scheme} (AC+DC). " +
                        "NOTE: this modified the user's currently active Windows power plan.");
                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal((IntPtr)schemePtr);
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: processor min state restore failed.", ex);
            return false;
        }
    }

    private bool TryResetRgbKeyboard(StringBuilder sb)
    {
        sb.Append("[rgb-keyboard-backlight] ");

        try
        {
            if (_impl.TryResolve(typeof(RGBKeyboardBacklightController)) is not RGBKeyboardBacklightController rgb)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            rgb.SetLightControlOwnerAsync(false).GetAwaiter().GetResult();
            sb.AppendLine("disabled (light control owner released).");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: RGB reset failed.", ex);
            return false;
        }
    }

    private bool TryStopSpectrum(StringBuilder sb)
    {
        sb.Append("[spectrum-keyboard-backlight] ");

        try
        {
            if (_impl.TryResolve(typeof(SpectrumKeyboardBacklightController)) is not SpectrumKeyboardBacklightController spectrum)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            spectrum.Dispose();
            sb.AppendLine("disposed.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: Spectrum stop failed.", ex);
            return false;
        }
    }

    private bool TryDisableLenovoLighting(StringBuilder sb)
    {
        var features = new (string DisplayName, Type Type)[]
        {
            ("WhiteKeyboardLenovoLightingBacklightFeature",
                Type.GetType("UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight.WhiteKeyboardLenovoLightingBacklightFeature, UniversalDeviceToolkit.Lib", throwOnError: false)
                ?? typeof(object)),
            ("PanelLogoLenovoLightingBacklightFeature",
                Type.GetType("UniversalDeviceToolkit.Lib.Features.PanelLogo.PanelLogoLenovoLightingBacklightFeature, UniversalDeviceToolkit.Lib", throwOnError: false)
                ?? typeof(object)),
            ("PortsBacklightFeature",
                Type.GetType("UniversalDeviceToolkit.Lib.Features.PortsBacklightFeature, UniversalDeviceToolkit.Lib", throwOnError: false)
                ?? typeof(object)),
        };

        var overall = true;
        var any = false;
        foreach (var (displayName, type) in features)
        {
            sb.Append($"[{displayName}] ");
            if (type is null || type == typeof(object))
            {
                sb.AppendLine("skipped (component not initialized).");
                continue;
            }

            try
            {
                var resolved = _impl.TryResolve(type);
                if (resolved is null)
                {
                    sb.AppendLine("skipped (component not initialized).");
                    continue;
                }

                var forceDisableProperty = resolved.GetType().GetProperty("ForceDisable");
                if (forceDisableProperty is null || !forceDisableProperty.CanWrite)
                {
                    sb.AppendLine($"skipped (no ForceDisable property on '{resolved.GetType().Name}').");
                    continue;
                }

                forceDisableProperty.SetValue(resolved, true);
                any = true;
                sb.AppendLine("disabled (ForceDisable=true).");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
                TryTrace($"HardwareStateRecoveryService: lighting feature '{displayName}' reset failed.", ex);
                overall = false;
            }
        }

        if (!any && overall)
            sb.AppendLine("[lenovo-lighting] skipped (no lighting components initialized).");

        return overall;
    }

    private bool TryDisableExperimentalGpu(StringBuilder sb)
    {
        sb.Append("[experimental-gpu-working-mode] ");
        try
        {
            if (_impl.TryResolve(typeof(IGPUModeFeature)) is not IGPUModeFeature gpu)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            gpu.ExperimentalGPUWorkingMode = false;
            sb.AppendLine("disabled.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: experimental GPU mode reset failed.", ex);
            return false;
        }
    }

    private bool TryClearPowerModeCustomizations(StringBuilder sb)
    {
        sb.Append("[power-mode-customizations] ");
        try
        {
            if (_impl.TryResolve(typeof(ApplicationSettings)) is not ApplicationSettings appSettings)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            var store = appSettings.Store;
            if (store is null)
            {
                sb.AppendLine("skipped (settings store not loaded).");
                return true;
            }

            var cleared = false;
            if (store.PowerPlans is { Count: > 0 })
            {
                store.PowerPlans.Clear();
                cleared = true;
            }
            if (store.PowerModes is { Count: > 0 })
            {
                store.PowerModes.Clear();
                cleared = true;
            }

            if (cleared)
                appSettings.SynchronizeStore();

            sb.AppendLine(cleared
                ? "cleared and persisted."
                : "no customizations to clear.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: power mode reset failed.", ex);
            return false;
        }
    }

    private bool TryResetFanCurves(StringBuilder sb)
    {
        sb.Append("[fan-curve-settings] ");
        try
        {
            if (_impl.TryResolve(typeof(FanCurveSettings)) is not FanCurveSettings fanSettings)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            var store = fanSettings.Store;
            if (store?.Entries is null || store.Entries.Count == 0)
            {
                sb.AppendLine("no entries to clear.");
                return true;
            }

            var removed = store.Entries.Count;
            store.Entries.Clear();
            fanSettings.SynchronizeStore();
            sb.AppendLine($"cleared {removed} entries and persisted.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: fan curve reset failed.", ex);
            return false;
        }
    }

    private bool TryResetBalanceMode(StringBuilder sb)
    {
        sb.Append("[balance-mode-settings] ");
        try
        {
            if (_impl.TryResolve(typeof(BalanceModeSettings)) is not BalanceModeSettings balanceSettings)
            {
                sb.AppendLine("skipped (component not initialized).");
                return true;
            }

            var store = balanceSettings.Store;
            if (store is null)
            {
                sb.AppendLine("skipped (settings store not loaded).");
                return true;
            }

            if (!store.AIModeEnabled)
            {
                sb.AppendLine("already default.");
                return true;
            }

            store.AIModeEnabled = false;
            balanceSettings.SynchronizeStore();
            sb.AppendLine("reset to default and persisted.");
            return true;
        }
        catch (Exception ex)
        {
            sb.AppendLine($"failure ({ex.GetType().Name}: {ex.Message}).");
            TryTrace("HardwareStateRecoveryService: BalanceMode reset failed.", ex);
            return false;
        }
    }

    private static string[] StripProxyArgs(string[] lines)
    {
        if (lines is null || lines.Length == 0)
            return Array.Empty<string>();

        var proxyPrefixes = new[]
        {
            "--proxy-url",
            "--proxy-username",
            "--proxy-password",
            "--proxy-allow-all-certs",
        };

        var filtered = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i] ?? string.Empty;
            var trimmed = raw.TrimStart();
            var matched = proxyPrefixes.Any(p =>
                trimmed.StartsWith(p + " ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(p + "=", StringComparison.OrdinalIgnoreCase));

            if (!matched)
            {
                filtered.Add(raw);
                continue;
            }

            if (trimmed.Equals(proxyPrefixes[0], StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals(proxyPrefixes[1], StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals(proxyPrefixes[2], StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < lines.Length)
                {
                    var next = (lines[i + 1] ?? string.Empty).TrimStart();
                    if (!next.StartsWith("--", StringComparison.Ordinal))
                        i++;
                }
            }
        }

        return filtered.ToArray();
    }

    private void TryWriteConsole(string report) => _impl.Console?.Invoke(report);

    private void TryTrace(string message, Exception? ex = null)
    {
        var trace = _impl.Trace;
        if (trace is null)
            return;
        try
        {
            trace(message + (ex is null ? string.Empty : $" {ex.GetType().Name}: {ex.Message}"));
        }
        catch
        {
            // Tracing must never propagate. We have already written the report
            // to the console sink; an absent trace facility must not break the
            // recovery contract.
        }
    }
}
