#if !WINDOWS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Lib;
#if LINUX
using UniversalDeviceToolkit.Platform.Linux.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;
#endif

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Portable feature surface: only backends that exist on this OS are reported
/// as supported. Lenovo EC / WMI / vendor lighting stay unsupported.
/// </summary>
internal static class PortableFeatureSupport
{
    public static bool IsSupported(string feature) => feature switch
    {
        "powerMode" => PowerProvider() is { IsAvailable: true },
        "battery" => ChargeThresholdAvailable(),
        _ => false
    };

    public static string StateType(string feature) => feature switch
    {
        "powerMode" => "PowerModeState",
        "battery" => "BatteryState",
        _ => "Unsupported"
    };

    public static Task<BridgeResult> GetStatesAsync(string feature, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (feature == "powerMode")
        {
            var provider = PowerProvider();
            if (provider is not { IsAvailable: true })
                return Task.FromResult(Unsupported());
            var states = provider.GetAvailableProfiles()
                .Select(MapPowerProfileToState)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (states.Length == 0)
                states = ["Quiet", "Balance", "Performance"];
            return Task.FromResult(BridgeResult.Ok(new { states }));
        }

        if (feature == "battery")
        {
            if (!ChargeThresholdAvailable())
                return Task.FromResult(Unsupported());
            return Task.FromResult(BridgeResult.Ok(new { states = new[] { "Conservation", "Normal" } }));
        }

        return Task.FromResult(Unsupported());
    }

    public static Task<BridgeResult> GetStateAsync(string feature, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (feature == "powerMode")
        {
            var provider = PowerProvider();
            if (provider is not { IsAvailable: true })
                return Task.FromResult(Unsupported());
            var active = provider.GetActiveProfile();
            if (string.IsNullOrWhiteSpace(active))
                return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, "No active Linux power profile was reported."));
            return Task.FromResult(BridgeResult.Ok(new { state = MapPowerProfileToState(active) }));
        }

        if (feature == "battery")
        {
#if LINUX
            var threshold = LinuxPowerSupplyReader.ReadChargeThreshold(PhysicalLinuxFileSystem.Instance);
            if (threshold?.EndPercent is null)
                return Task.FromResult(Unsupported());
            var state = threshold.EndPercent.Value <= 80 ? "Conservation" : "Normal";
            return Task.FromResult(BridgeResult.Ok(new { state }));
#else
            return Task.FromResult(Unsupported());
#endif
        }

        return Task.FromResult(Unsupported());
    }

    public static async Task<BridgeResult> SetStateAsync(string feature, JsonElement parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!parameters.TryGetProperty("state", out var stateProp))
            return BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing 'state' parameter.");

        var state = stateProp.ValueKind == JsonValueKind.String
            ? stateProp.GetString()
            : stateProp.GetRawText();
        if (string.IsNullOrWhiteSpace(state))
            return BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Feature state must be a non-empty string.");

        if (feature == "powerMode")
        {
            var provider = PowerProvider();
            if (provider is not { IsAvailable: true })
                return Unsupported();

            var profile = MapStateToPowerProfile(state, provider.GetAvailableProfiles());
            if (profile is null)
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.InvalidParams,
                    $"Power profile '{state}' is not available on this system.");
            }

            await provider.SetActiveProfileAsync(profile).ConfigureAwait(false);
            var applied = provider.GetActiveProfile();
            var expected = MapPowerProfileToState(profile);
            var actual = applied is null ? null : MapPowerProfileToState(applied);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(applied, profile, StringComparison.OrdinalIgnoreCase))
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.InternalError,
                    "Power profile change did not persist.");
            }

            return BridgeResult.Ok(new { ok = true });
        }

        if (feature == "battery")
        {
#if LINUX
            if (!ChargeThresholdAvailable())
                return Unsupported();

            var percent = state.Equals("Conservation", StringComparison.OrdinalIgnoreCase) ? 80
                : state.Equals("Normal", StringComparison.OrdinalIgnoreCase) ? 100
                : (int?)null;
            if (percent is null)
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.InvalidParams,
                    "Linux battery charge threshold supports Conservation or Normal.");
            }

            if (!LinuxPowerSupplyReader.TryWriteChargeEndThreshold(PhysicalLinuxFileSystem.Instance, percent.Value, out var error))
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.InternalError,
                    error ?? "Failed to write charge_control_end_threshold.");
            }

            return BridgeResult.Ok(new { ok = true });
#else
            return Unsupported();
#endif
        }

        return Unsupported();
    }

    private static IPowerProfileProvider? PowerProvider() =>
        IoCContainer.TryResolve<IPowerProfileProvider>();

    private static bool ChargeThresholdAvailable() =>
#if LINUX
        LinuxPowerSupplyReader.ReadChargeThreshold(PhysicalLinuxFileSystem.Instance) is not null;
#else
        false;
#endif

    internal static string MapPowerProfileToState(string profile)
    {
        if (Contains(profile, "power-saver", "powersave", "power_saver", "quiet", "saver", "eco"))
            return "Quiet";
        if (Contains(profile, "performance", "high", "turbo"))
            return "Performance";
        if (Contains(profile, "balance", "balanced", "mid"))
            return "Balance";
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(profile.Replace('-', ' ').Replace('_', ' '));
    }

    internal static string? MapStateToPowerProfile(string state, IReadOnlyList<string> profiles)
    {
        var exact = profiles.FirstOrDefault(profile => profile.Equals(state, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var mapped = profiles.FirstOrDefault(profile =>
            MapPowerProfileToState(profile).Equals(state, StringComparison.OrdinalIgnoreCase));
        return mapped;
    }

    private static bool Contains(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static BridgeResult Unsupported() =>
        BridgeResult.Error(
            BridgeErrorCodes.FeatureNotSupported,
            "This hardware feature is not available on this platform.");
}
#endif
