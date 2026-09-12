using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Management;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;

// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.Utils;

public static partial class Compatibility
{
    [GeneratedRegex("^[A-Z0-9]{4}")]
    private static partial Regex BiosPrefixRegex();

    [GeneratedRegex("[0-9]{2}")]
    private static partial Regex BiosVersionRegex();

    private static readonly Dictionary<string, LegionSeries> MachineTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Legion 5 / Y7000 / R7000 (2020–2026)
        { "83F0", LegionSeries.Legion_5 }, { "83F1", LegionSeries.Legion_5 }, { "83M0", LegionSeries.Legion_5 },
        { "83NX", LegionSeries.Legion_5 }, { "83N2", LegionSeries.Legion_5 }, { "83LY", LegionSeries.Legion_5 },
        { "83EW", LegionSeries.Legion_5 }, { "83EG", LegionSeries.Legion_5 },
        { "83JJ", LegionSeries.Legion_5 }, { "83JG", LegionSeries.Legion_5 }, { "83JH", LegionSeries.Legion_5 },
        { "82RC", LegionSeries.Legion_5 }, { "82RB", LegionSeries.Legion_5 },
        { "82TB", LegionSeries.Legion_5 }, { "83EF", LegionSeries.Legion_5 }, { "82RE", LegionSeries.Legion_5 },
        { "82RD", LegionSeries.Legion_5 }, { "82AX", LegionSeries.Legion_5 }, { "82B0", LegionSeries.Legion_5 },
        { "82GR", LegionSeries.Legion_5 }, { "82JU", LegionSeries.Legion_5 }, { "82JY", LegionSeries.Legion_5 },
        { "82K0", LegionSeries.Legion_5 }, { "82K1", LegionSeries.Legion_5 }, { "82K2", LegionSeries.Legion_5 },
        { "82N4", LegionSeries.Legion_5 }, { "82N5", LegionSeries.Legion_5 }, { "82NW", LegionSeries.Legion_5 },
        { "83FG", LegionSeries.Legion_5 }, { "83FH", LegionSeries.Legion_5 }, { "83G1", LegionSeries.Legion_5 },
        { "83G2", LegionSeries.Legion_5 }, { "83LL", LegionSeries.Legion_5 }, { "83LM", LegionSeries.Legion_5 },
        { "83LN", LegionSeries.Legion_5 }, { "83DT", LegionSeries.Legion_5 }, { "83DU", LegionSeries.Legion_5 },
        { "83C6", LegionSeries.Legion_5 }, { "83C7", LegionSeries.Legion_5 },

        // Slim 5
        { "83DH", LegionSeries.Legion_Slim_5 }, { "83EX", LegionSeries.Legion_Slim_5 }, { "82Y5", LegionSeries.Legion_Slim_5 },
        { "82Y9", LegionSeries.Legion_Slim_5 }, { "82YA", LegionSeries.Legion_Slim_5 }, { "83D6", LegionSeries.Legion_Slim_5 },
        { "83D0", LegionSeries.Legion_Slim_5 }, { "83D1", LegionSeries.Legion_Slim_5 },
        { "83G5", LegionSeries.Legion_Slim_5 }, { "83G6", LegionSeries.Legion_Slim_5 },
        { "83G7", LegionSeries.Legion_Slim_5 }, { "83G8", LegionSeries.Legion_Slim_5 },

        // Pro 5 / Y9000P / R9000P (includes CN 83DF IRX9)
        { "83LT", LegionSeries.Legion_Pro_5 }, { "83F3", LegionSeries.Legion_Pro_5 }, { "83DF", LegionSeries.Legion_Pro_5 },
        { "83F2", LegionSeries.Legion_Pro_5 }, { "83LU", LegionSeries.Legion_Pro_5 }, { "82WM", LegionSeries.Legion_Pro_5 },
        { "83NN", LegionSeries.Legion_Pro_5 }, { "82WK", LegionSeries.Legion_Pro_5 }, { "82JQ", LegionSeries.Legion_Pro_5 },
        { "82RF", LegionSeries.Legion_Pro_5 }, { "82RG", LegionSeries.Legion_Pro_5 }, { "83DG", LegionSeries.Legion_Pro_5 },
        { "83LR", LegionSeries.Legion_Pro_5 }, { "83LS", LegionSeries.Legion_Pro_5 }, { "83LV", LegionSeries.Legion_Pro_5 },
        { "83LW", LegionSeries.Legion_Pro_5 }, { "83LX", LegionSeries.Legion_Pro_5 }, { "82SN", LegionSeries.Legion_Pro_5 },
        { "82SM", LegionSeries.Legion_Pro_5 },

        // Legion 7
        { "83KY", LegionSeries.Legion_7 }, { "83FD", LegionSeries.Legion_7 }, { "82UH", LegionSeries.Legion_7 },
        { "82TD", LegionSeries.Legion_7 }, { "82N6", LegionSeries.Legion_7 }, { "82N7", LegionSeries.Legion_7 },
        { "83FE", LegionSeries.Legion_7 }, { "83FF", LegionSeries.Legion_7 }, { "83K0", LegionSeries.Legion_7 },
        { "83K1", LegionSeries.Legion_7 }, { "83AG", LegionSeries.Legion_7 },

        // Pro 7 / top-tier Y9000P
        { "83RU", LegionSeries.Legion_Pro_7 }, { "83F5", LegionSeries.Legion_Pro_7 }, { "83DE", LegionSeries.Legion_Pro_7 },
        { "82WR", LegionSeries.Legion_Pro_7 }, { "82WQ", LegionSeries.Legion_Pro_7 }, { "82WS", LegionSeries.Legion_Pro_7 },
        { "82WT", LegionSeries.Legion_Pro_7 }, { "83RS", LegionSeries.Legion_Pro_7 }, { "83RT", LegionSeries.Legion_Pro_7 },
        { "83RV", LegionSeries.Legion_Pro_7 }, { "83RW", LegionSeries.Legion_Pro_7 }, { "83RX", LegionSeries.Legion_Pro_7 },
        { "83RY", LegionSeries.Legion_Pro_7 },

        // Legion 9
        { "83G0", LegionSeries.Legion_9 }, { "83EY", LegionSeries.Legion_9 }, { "83EZ", LegionSeries.Legion_9 },
        { "83G9", LegionSeries.Legion_9 }, { "83GA", LegionSeries.Legion_9 }, { "83GB", LegionSeries.Legion_9 },

        // Legion Go / LOQ
        { "83E1", LegionSeries.Legion_Go }, { "83N0", LegionSeries.Legion_Go }, { "83N1", LegionSeries.Legion_Go },
        { "83L3", LegionSeries.Legion_Go },
        { "83GS", LegionSeries.LOQ }, { "83GT", LegionSeries.LOQ }, { "83GU", LegionSeries.LOQ },
        { "83GV", LegionSeries.LOQ }, { "83GW", LegionSeries.LOQ }, { "83JC", LegionSeries.LOQ },
        { "83JD", LegionSeries.LOQ }, { "83JE", LegionSeries.LOQ }, { "83JF", LegionSeries.LOQ },
        { "82XV", LegionSeries.LOQ }, { "82XW", LegionSeries.LOQ }, { "83DV", LegionSeries.LOQ },
        { "83DW", LegionSeries.LOQ }, { "83DX", LegionSeries.LOQ }, { "83DY", LegionSeries.LOQ },
        { "83AQ", LegionSeries.LOQ }, { "83AR", LegionSeries.LOQ }, { "83AS", LegionSeries.LOQ },
    };

    private static readonly (string Keyword, LegionSeries Series)[] ModelKeywordMap =
    [
        // Longer / more specific names first
        ("Y9000P", LegionSeries.Legion_Pro_5),
        ("R9000P", LegionSeries.Legion_Pro_5),
        ("Y9000K", LegionSeries.Legion_Pro_7),
        ("R9000K", LegionSeries.Legion_Pro_7),
        ("Y7000P", LegionSeries.Legion_5),
        ("R7000P", LegionSeries.Legion_5),
        ("Y7000", LegionSeries.Legion_5),
        ("R7000", LegionSeries.Legion_5),
        ("拯救者", LegionSeries.Legion_Legacy),
        ("LOQ", LegionSeries.LOQ),
        ("IdeaPad Gaming", LegionSeries.IdeaPad_Gaming),
        ("IdeaPad", LegionSeries.IdeaPad),
        ("XiaoXin", LegionSeries.IdeaPad),
        ("YOGA", LegionSeries.YOGA),
        ("Lenovo Slim", LegionSeries.Lenovo_Slim),
        ("ThinkBook", LegionSeries.ThinkBook),
        ("Legion", LegionSeries.Legion_Legacy)
    ];

    private static Lazy<Task<MachineInformation>> _machineInformationLazy = new(GetMachineInformationInternalAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    // TOCTOU protection: reference assignment is atomic in .NET; the race is
    // benign because the value is idempotent (always set to true). We use a
    // local snapshot to ensure a consistent read of HasValue + Value.
    private static bool? _isCompatible;

    public static Task<bool> CheckBasicCompatibilityAsync() => WMI.LenovoGameZoneData.ExistsAsync();

    public static DeviceFeatureAvailability GetDeviceFeatureAvailability(MachineInformation machineInformation) =>
        LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

    public static bool IsSupportedDevice(MachineInformation machineInformation) =>
        GetDeviceFeatureAvailability(machineInformation).IsSupported;

    public static bool IsSupportedLegionMachine(MachineInformation machineInformation) =>
        IsSupportedDevice(machineInformation);

    public static async Task<(bool isCompatible, MachineInformation machineInformation)> IsCompatibleAsync()
    {
        var mi = await GetMachineInformationAsync().ConfigureAwait(false);

        var cachedIsCompatible = _isCompatible;
        if (cachedIsCompatible.HasValue)
            return (cachedIsCompatible.Value, mi);

        var isSupportedLenovoDevice = IsSupportedLegionMachine(mi);
        if (isSupportedLenovoDevice && !await CheckBasicCompatibilityAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Supported Lenovo device detected without LenovoGameZoneData; continuing in basic mode.");
        }

        _isCompatible = true;
        return (_isCompatible.Value, mi);
    }

    public static bool IsCompatible => _isCompatible ?? false;

    public static Task<MachineInformation> GetMachineInformationAsync() => _machineInformationLazy.Value;

    private static async Task<MachineInformation> GetMachineInformationInternalAsync()
    {
        var (vendor, machineType, model, serialNumber) = await GetModelDataAsync().ConfigureAwait(false);
        var hardware = await HardwareInventoryProvider.ReadAsync().ConfigureAwait(false);
        var generation = GetMachineGeneration(model);
        var legionSeries = GetLegionSeries(model, machineType);
        var (biosVersion, biosVersionRaw) = GetBIOSVersion();
        var supportedPowerModes = (await GetSupportedPowerModesAsync().ConfigureAwait(false)).ToArray();
        var smartFanVersion = await GetSmartFanVersionAsync().ConfigureAwait(false);
        var legionZoneVersion = await GetLegionZoneVersionAsync().ConfigureAwait(false);
        var features = await GetFeaturesAsync().ConfigureAwait(false);

        var properties = new MachineInformation.PropertyData
        {
            SupportsAlwaysOnAc = GetAlwaysOnAcStatus(),
            SupportsGodModeV1 = GetSupportsGodModeV1(supportedPowerModes, smartFanVersion, legionZoneVersion, biosVersion),
            SupportsGodModeV2 = GetSupportsGodModeV2(supportedPowerModes, smartFanVersion, legionZoneVersion, features),
            SupportsGodModeV3 = GetSupportsGodModeV3(supportedPowerModes, smartFanVersion, legionZoneVersion, generation, model, machineType),
            SupportsGodModeV4 = GetSupportsGodModeV4(supportedPowerModes, smartFanVersion, legionZoneVersion),
            SupportsGSync = await GetSupportsGSyncAsync(features).ConfigureAwait(false),
            SupportsIGPUMode = await GetSupportsIGPUModeAsync(features).ConfigureAwait(false),
            SupportsAIMode = await GetSupportsAIModeAsync().ConfigureAwait(false),
            SupportBootLogoChange = GetSupportBootLogoChange(smartFanVersion),
            SupportsITSMode = GetSupportITSMode(model),
            HasQuietToPerformanceModeSwitchingBug = GetHasQuietToPerformanceModeSwitchingBug(biosVersion),
            HasGodModeToOtherModeSwitchingBug = GetHasGodModeToOtherModeSwitchingBug(biosVersion),
            HasReapplyParameterIssue = GetHasReapplyParameterIssue(model, machineType),
            HasSpectrumProfileSwitchingBug = GetHasSpectrumProfileSwitchingBug(model, machineType),
            IsExcludedFromLenovoLighting = GetIsExcludedFromLenovoLighting(biosVersion, generation, legionSeries),
            IsExcludedFromPanelLogoLenovoLighting = GetIsExcludedFromPanelLenovoLighting(machineType, model),
            HasAlternativeFullSpectrumLayout = GetHasAlternativeFullSpectrumLayout(machineType),
            IsAmdDevice = GetIsAmdDevice(model),
            IsChineseModel = GetIsChineseModel(model),
        };

        properties = ApplyGodModeFallback(machineType, model, supportedPowerModes, features, properties);

        var machineInformation = new MachineInformation
        {
            Generation = generation,
            LegionSeries = legionSeries,
            Vendor = vendor,
            MachineType = machineType,
            Model = model,
            SerialNumber = serialNumber,
            BiosVersion = biosVersion,
            BiosVersionRaw = biosVersionRaw,
            SupportedPowerModes = supportedPowerModes,
            SmartFanVersion = smartFanVersion,
            LegionZoneVersion = legionZoneVersion,
            Features = features,
            Hardware = hardware,
            Properties = properties
        };

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Retrieved machine information:");
            Log.Instance.Trace($" * Vendor: '{machineInformation.Vendor}'");
            Log.Instance.Trace($" * Machine Type: '{machineInformation.MachineType}'");
            Log.Instance.Trace($" * Model: '{machineInformation.Model}'");
            Log.Instance.Trace($" * BIOS: '{machineInformation.BiosVersion}' [{machineInformation.BiosVersionRaw}]");
            Log.Instance.Trace($" * SupportedPowerModes: '{string.Join(",", machineInformation.SupportedPowerModes)}'");
            Log.Instance.Trace($" * SmartFanVersion: '{machineInformation.SmartFanVersion}'");
            Log.Instance.Trace($" * LegionZoneVersion: '{machineInformation.LegionZoneVersion}'");
            Log.Instance.Trace($" * Features: {machineInformation.Features.Source}:{string.Join(',', machineInformation.Features.All)}");
            LogHardwareInventory(machineInformation.Hardware);
            Log.Instance.Trace($" * Properties:");
            Log.Instance.Trace($"     * SupportsAlwaysOnAc: '{machineInformation.Properties.SupportsAlwaysOnAc.status}, {machineInformation.Properties.SupportsAlwaysOnAc.connectivity}'");
            Log.Instance.Trace($"     * SupportsGodModeV1: '{machineInformation.Properties.SupportsGodModeV1}'");
            Log.Instance.Trace($"     * SupportsGodModeV2: '{machineInformation.Properties.SupportsGodModeV2}'");
            Log.Instance.Trace($"     * SupportsGodModeV3: '{machineInformation.Properties.SupportsGodModeV3}'");
            Log.Instance.Trace($"     * SupportsGodModeV4: '{machineInformation.Properties.SupportsGodModeV4}'");
            Log.Instance.Trace($"     * SupportsGSync: '{machineInformation.Properties.SupportsGSync}'");
            Log.Instance.Trace($"     * SupportsIGPUMode: '{machineInformation.Properties.SupportsIGPUMode}'");
            Log.Instance.Trace($"     * SupportsAIMode: '{machineInformation.Properties.SupportsAIMode}'");
            Log.Instance.Trace($"     * SupportsITSMode: '{machineInformation.Properties.SupportsITSMode}'");
            Log.Instance.Trace($"     * SupportBootLogoChange: '{machineInformation.Properties.SupportBootLogoChange}'");
            Log.Instance.Trace($"     * HasQuietToPerformanceModeSwitchingBug: '{machineInformation.Properties.HasQuietToPerformanceModeSwitchingBug}'");
            Log.Instance.Trace($"     * HasGodModeToOtherModeSwitchingBug: '{machineInformation.Properties.HasGodModeToOtherModeSwitchingBug}'");
            Log.Instance.Trace($"     * HasReapplyParameterIssue: '{machineInformation.Properties.HasReapplyParameterIssue}'");
            Log.Instance.Trace($"     * HasSpectrumProfileSwitchingBug: '{machineInformation.Properties.HasSpectrumProfileSwitchingBug}'");
            Log.Instance.Trace($"     * IsExcludedFromLenovoLighting: '{machineInformation.Properties.IsExcludedFromLenovoLighting}'");
            Log.Instance.Trace($"     * IsExcludedFromPanelLogoLenovoLighting: '{machineInformation.Properties.IsExcludedFromPanelLogoLenovoLighting}'");
            Log.Instance.Trace($"     * HasAlternativeFullSpectrumLayout: '{machineInformation.Properties.HasAlternativeFullSpectrumLayout}'");
            Log.Instance.Trace($"     * IsAmdDevice: '{machineInformation.Properties.IsAmdDevice}'");
            Log.Instance.Trace($"     * IsChineseModel: '{machineInformation.Properties.IsChineseModel}'");
        }

        return machineInformation;
    }

    private static MachineInformation.PropertyData ApplyGodModeFallback(
        string machineType,
        string model,
        IEnumerable<PowerModeState> supportedPowerModes,
        MachineInformation.FeatureData features,
        MachineInformation.PropertyData properties)
    {
        if (properties.SupportsGodMode)
            return properties;

        if (supportedPowerModes.Contains(PowerModeState.GodMode))
            return properties with { SupportsGodModeV2 = true };

        if (features.Source == MachineInformation.FeatureData.SourceType.CapabilityData
            && features[CapabilityID.GodModeFnQSwitchable])
            return properties with { SupportsGodModeV2 = true };

        var availability = GetDeviceFeatureAvailability(new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = machineType,
            Model = model,
            SupportedPowerModes = [],
            Features = MachineInformation.FeatureData.Unknown,
            Properties = new MachineInformation.PropertyData()
        });

        if (availability.IsBasicMode)
            return properties;

        return availability.DevicePackId switch
        {
            "lenovo-legion-pro-5" or "lenovo-legion-pro-7" or "lenovo-legion-9" or "lenovo-legion-7" or "lenovo-legion-5" or "lenovo-legacy-limited" => properties with { SupportsGodModeV2 = true },
            _ => properties
        };
    }

    public static bool SupportsGodModeCustomization(MachineInformation machineInformation)
    {
        var supportedPowerModes = machineInformation.SupportedPowerModes ?? [];

        return machineInformation.Properties.SupportsGodMode
               || supportedPowerModes.Contains(PowerModeState.GodMode)
               || (machineInformation.Features.Source == MachineInformation.FeatureData.SourceType.CapabilityData
                   && machineInformation.Features[CapabilityID.GodModeFnQSwitchable]);
    }

    private static void LogHardwareInventory(HardwareInventory hardware)
    {
        Log.Instance.Trace($" * Hardware:");
        Log.Instance.Trace($"     * ComputerSystem: '{hardware.ComputerSystem.Manufacturer}' '{hardware.ComputerSystem.Model}' '{hardware.ComputerSystem.SystemFamily}' '{hardware.ComputerSystem.SystemType}'");
        Log.Instance.Trace($"     * BaseBoard: '{hardware.BaseBoard.Manufacturer}' '{hardware.BaseBoard.Product}' '{hardware.BaseBoard.Version}'");
        Log.Instance.Trace($"     * Chassis: '{hardware.Chassis.Manufacturer}' '{string.Join(",", hardware.Chassis.ChassisTypeNames)}'");
        Log.Instance.Trace($"     * CPU: '{string.Join(" | ", hardware.Processors.Select(processor => processor.Name).Where(name => !string.IsNullOrWhiteSpace(name)))}'");
        Log.Instance.Trace($"     * GPU: '{string.Join(" | ", hardware.VideoControllers.Select(videoController => videoController.Name).Where(name => !string.IsNullOrWhiteSpace(name)))}'");
        Log.Instance.Trace($"     * Memory: '{FormatCapacity(hardware.Memory.TotalCapacityBytes)}' modules='{hardware.Memory.ModuleCount}' speed='{hardware.Memory.ConfiguredClockSpeedMHz ?? hardware.Memory.SpeedMHz}'");
        Log.Instance.Trace($"     * Battery: '{string.Join(" | ", hardware.Batteries.Select(battery => battery.Name).Where(name => !string.IsNullOrWhiteSpace(name)))}'");
    }

    private static Task<(string, string, string, string)> GetModelDataAsync() => WMI.Win32.ComputerSystemProduct.ReadAsync();

    private static (BiosVersion?, string?) GetBIOSVersion()
    {
        var result = Registry.GetValue("HKEY_LOCAL_MACHINE", "HARDWARE\\DESCRIPTION\\System\\BIOS", "BIOSVersion", string.Empty).Trim();

        var prefixRegex = BiosPrefixRegex();
        var versionRegex = BiosVersionRegex();

        var prefix = prefixRegex.Match(result).Value;
        var versionString = versionRegex.Match(result).Value;

        if (!int.TryParse(versionRegex.Match(versionString).Value, out var version))
            return (null, null);

        return (new(prefix, version), result);
    }

    private static bool GetIsChineseModel(string model)
    {
        string[] chineseModelIndicators =
        [
            "R7000",
            "R9000",
            "Y7000",
            "Y9000"
        ];

        return chineseModelIndicators.Any(model.Contains);
    }

    private static bool GetIsAmdDevice(string model)
    {
        if (string.IsNullOrEmpty(model))
            return false;

        var normalizedModel = model.ToUpperInvariant();
        var match = Regex.Match(normalizedModel, @"([AI][A-Z]{2}\d+|\bR\d{4})", RegexOptions.RightToLeft);

        if (!match.Success)
            return false;

        var value = match.Value;
        return value.StartsWith("A") || value.StartsWith("R");
    }

    private static string FormatCapacity(ulong bytes)
    {
        if (bytes == 0)
            return string.Empty;

        const double gibibyte = 1024d * 1024d * 1024d;
        return $"{bytes / gibibyte:0.#} GiB";
    }

    private static async Task<MachineInformation.FeatureData> GetFeaturesAsync()
    {
        try
        {
            var capabilities = await WMI.LenovoCapabilityData00.ReadAsync().ConfigureAwait(false);
            return new(MachineInformation.FeatureData.SourceType.CapabilityData, capabilities);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"LenovoCapabilityData00 read failed, falling back to feature flags", ex);
        }

        try
        {
            var featureFlags = await WMI.LenovoOtherMethod.GetLegionDeviceSupportFeatureAsync().ConfigureAwait(false);

            return new(MachineInformation.FeatureData.SourceType.Flags)
            {
                [CapabilityID.IGPUMode] = featureFlags.IsBitSet(0),
                [CapabilityID.NvidiaGPUDynamicDisplaySwitching] = featureFlags.IsBitSet(4),
                [CapabilityID.InstantBootAc] = featureFlags.IsBitSet(5),
                [CapabilityID.InstantBootUsbPowerDelivery] = featureFlags.IsBitSet(6),
                [CapabilityID.AMDSmartShiftMode] = featureFlags.IsBitSet(7),
                [CapabilityID.AMDSkinTemperatureTracking] = featureFlags.IsBitSet(8),
                [CapabilityID.FlipToStart] = true,
                [CapabilityID.OverDrive] = true
            };
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GetLegionDeviceSupportFeature read failed, returning Unknown features", ex);
        }

        return MachineInformation.FeatureData.Unknown;
    }

    private static async Task<IEnumerable<PowerModeState>> GetSupportedPowerModesAsync()
    {
        try
        {
            var powerModes = new List<PowerModeState>();

            var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.SupportedPowerModes).ConfigureAwait(false);

            if (value.IsBitSet(0))
                powerModes.Add(PowerModeState.Quiet);
            if (value.IsBitSet(1))
                powerModes.Add(PowerModeState.Balance);
            if (value.IsBitSet(2))
                powerModes.Add(PowerModeState.Performance);
            if (value.IsBitSet(16))
                powerModes.Add(PowerModeState.GodMode);

            return powerModes;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GetFeatureValue(SupportedPowerModes) read failed, falling back to GetSupportThermalMode", ex);
        }

        try
        {
            var powerModes = new List<PowerModeState>();

            var result = await WMI.LenovoOtherMethod.GetSupportThermalModeAsync().ConfigureAwait(false);

            if (result.IsBitSet(0))
                powerModes.Add(PowerModeState.Quiet);
            if (result.IsBitSet(1))
                powerModes.Add(PowerModeState.Balance);
            if (result.IsBitSet(2))
                powerModes.Add(PowerModeState.Performance);
            if (result.IsBitSet(16))
                powerModes.Add(PowerModeState.GodMode);

            return powerModes;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GetSupportThermalMode read failed, returning empty power modes", ex);
        }

        return [];
    }

    private static async Task<int> GetSmartFanVersionAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.IsSupportSmartFanAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"IsSupportSmartFan read failed, returning -1", ex);
        }

        return -1;
    }

    private static async Task<int> GetLegionZoneVersionAsync()
    {
        try
        {
            return await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.LegionZoneSupportVersion).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GetFeatureValue(LegionZoneSupportVersion) read failed, falling back to GetSupportLegionZoneVersion", ex);
        }

        try
        {
            return await WMI.LenovoOtherMethod.GetSupportLegionZoneVersionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GetSupportLegionZoneVersion read failed, returning -1", ex);
        }

        return -1;
    }

    private static unsafe (bool status, bool connectivity) GetAlwaysOnAcStatus()
    {
        var capabilities = new SYSTEM_POWER_CAPABILITIES();
        var result = PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.SystemPowerCapabilities,
            null,
            0,
            &capabilities,
            (uint)Marshal.SizeOf<SYSTEM_POWER_CAPABILITIES>());

        // CallNtPowerInformation returns NTSTATUS: Success severity means the
        // SYSTEM_POWER_CAPABILITIES buffer is valid. The previous check inverted this
        // and always reported AoAc as unsupported on success.
        if (result.SeverityCode != NTSTATUS.Severity.Success)
            return (false, false);

        return (capabilities.AoAc, capabilities.AoAcConnectivitySupported);
    }

    private static bool GetSupportsGodModeV1(IEnumerable<PowerModeState> supportedPowerModes, int smartFanVersion, int legionZoneVersion, BiosVersion? biosVersion)
    {
        if (!supportedPowerModes.Contains(PowerModeState.GodMode))
            return false;

        var affectedBiosVersions = new BiosVersion[]
        {
            new("G9CN", 24),
            new("GKCN", 46),
            new("H1CN", 39),
            new("HACN", 31),
            new("HHCN", 20)
        };

        if (affectedBiosVersions.Any(bv => biosVersion?.IsLowerThan(bv) ?? false))
            return false;

        return smartFanVersion is 4 or 5 || legionZoneVersion is 1 or 2;
    }

    private static bool GetSupportsGodModeV2(
        IEnumerable<PowerModeState> supportedPowerModes,
        int smartFanVersion,
        int legionZoneVersion,
        MachineInformation.FeatureData features)
    {
        if (features.Source == MachineInformation.FeatureData.SourceType.CapabilityData
            && features[CapabilityID.GodModeFnQSwitchable])
            return true;

        if (!supportedPowerModes.Contains(PowerModeState.GodMode))
            return false;

        return smartFanVersion is 6 or 7 || legionZoneVersion is 3 or 4;
    }

    private static bool GetSupportsGodModeV3(IEnumerable<PowerModeState> supportedPowerModes, int smartFanVersion, int legionZoneVersion, int generation, string model, string machineType)
    {
        if (!supportedPowerModes.Contains(PowerModeState.GodMode))
            return false;

        var affectedSeries = new[]
        {
            LegionSeries.Legion_5,
            LegionSeries.Legion_7,
            LegionSeries.Legion_Pro_5,
            LegionSeries.Legion_Pro_7,
        };

        var affectedModels = new[]
        {
            "Legion 5",
            "Legion 7",
            "Legion Pro 5 16IAX10H",
            "LOQ",
            "Y7000",
            "Y7000P",
            "R7000",
            "R9000",
            "Y9000",
        };

        var isAffectedSeries = affectedSeries.Any(series => GetLegionSeries(model, machineType) == series);
        var isAffectedModel = affectedModels.Any(model.Contains);
        var isSupportedVersion = smartFanVersion is 8 or 9 || legionZoneVersion is 5 or 6;

        return (isAffectedSeries || isAffectedModel) && isSupportedVersion && generation >= 10;
    }

    private static bool GetSupportsGodModeV4(IEnumerable<PowerModeState> supportedPowerModes, int smartFanVersion, int legionZoneVersion)
    {
        if (!supportedPowerModes.Contains(PowerModeState.GodMode))
            return false;

        return smartFanVersion is 8 or 9 || legionZoneVersion is 5 or 6;
    }

    internal static bool FeatureDataIndicatesGSync(MachineInformation.FeatureData features) =>
        features.Source is MachineInformation.FeatureData.SourceType.CapabilityData
            or MachineInformation.FeatureData.SourceType.Flags
        && features[CapabilityID.NvidiaGPUDynamicDisplaySwitching];

    internal static bool FeatureDataIndicatesIgpuMode(MachineInformation.FeatureData features) =>
        features.Source is MachineInformation.FeatureData.SourceType.CapabilityData
            or MachineInformation.FeatureData.SourceType.Flags
        && features[CapabilityID.IGPUMode];

    internal static bool ResolveHybridGpuCapabilitySupport(
        int? gameZoneSupport,
        MachineInformation.FeatureData features,
        CapabilityID capabilityId)
        => capabilityId switch
        {
            CapabilityID.NvidiaGPUDynamicDisplaySwitching =>
                gameZoneSupport is > 0 || FeatureDataIndicatesGSync(features),
            CapabilityID.IGPUMode =>
                gameZoneSupport is > 0 || FeatureDataIndicatesIgpuMode(features),
            _ => false,
        };

    private static async Task<bool> GetSupportsGSyncAsync(MachineInformation.FeatureData features)
    {
        int? gameZoneSupport = null;
        try
        {
            gameZoneSupport = await WMI.LenovoGameZoneData.IsSupportGSyncAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("compat-gsync", "G-Sync support WMI probe failed.", ex);
        }

        return ResolveHybridGpuCapabilitySupport(
            gameZoneSupport,
            features,
            CapabilityID.NvidiaGPUDynamicDisplaySwitching);
    }

    private static async Task<bool> GetSupportsIGPUModeAsync(MachineInformation.FeatureData features)
    {
        int? gameZoneSupport = null;
        try
        {
            gameZoneSupport = await WMI.LenovoGameZoneData.IsSupportIGPUModeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("compat-igpu-mode", "iGPU mode support WMI probe failed.", ex);
        }

        return ResolveHybridGpuCapabilitySupport(
            gameZoneSupport,
            features,
            CapabilityID.IGPUMode);
    }

    private static async Task<bool> GetSupportsAIModeAsync()
    {
        try
        {
            await WMI.LenovoGameZoneData.GetIntelligentSubModeAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("compat-ai-mode", "AI mode support WMI probe failed.", ex);
            return false;
        }
    }

    private static bool GetSupportBootLogoChange(int smartFanVersion) => smartFanVersion < 8;

    private static bool GetSupportITSMode(string model)
    {
        var lower = model.ToLowerInvariant();

        if (lower.Contains("IdeaPad Gaming".ToLowerInvariant()))
            return false;

        return lower.Contains("IdeaPad".ToLowerInvariant())
            || lower.Contains("ThinkBook".ToLowerInvariant())
            || lower.Contains("Lenovo Slim".ToLowerInvariant());
    }

    private static int GetMachineGeneration(string model)
    {
        var platformMatch = Regex.Match(model, @"(?<=[A-Z]{3})(?<gen>\d{1,2})", RegexOptions.IgnoreCase);
        if (platformMatch.Success && int.TryParse(platformMatch.Groups["gen"].Value, out var platformGen))
            return platformGen;

        var generationMatch = Regex.Match(model, @"g(?<gen>\d+)", RegexOptions.IgnoreCase);
        if (generationMatch.Success && int.TryParse(generationMatch.Groups["gen"].Value, out var genValue))
            return genValue;

        var matches = Regex.Matches(model, @"(?<!\d)\d{1,2}(?!\d)");
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Value, out var value))
                continue;
            if (value >= 14 && value <= 18)
                continue;

            return value;
        }

        return 0;
    }

    private static LegionSeries GetLegionSeries(string model, string machineType)
    {
        if (MachineTypeMap.TryGetValue(machineType, out var series))
            return series;

        foreach (var (keyword, legionSeries) in ModelKeywordMap)
        {
            if (model.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return legionSeries;
        }

        return LegionSeries.Unknown;
    }

    private static bool GetHasQuietToPerformanceModeSwitchingBug(BiosVersion? biosVersion)
    {
        var affectedBiosVersions = new BiosVersion[]
        {
            new("J2CN", null)
        };

        return affectedBiosVersions.Any(bv => biosVersion?.IsHigherOrEqualThan(bv) ?? false);
    }

    private static bool GetHasGodModeToOtherModeSwitchingBug(BiosVersion? biosVersion)
    {
        var affectedBiosVersions = new BiosVersion[]
        {
            new("K1CN", null)
        };

        return affectedBiosVersions.Any(bv => biosVersion?.IsHigherOrEqualThan(bv) ?? false);
    }

    private static bool GetHasReapplyParameterIssue(string? machineModel, string machineType)
    {
        if (string.IsNullOrEmpty(machineModel))
            return false;

        var affectedSeries = new[]
        {
            LegionSeries.Legion_5,
            LegionSeries.Legion_7,
            LegionSeries.Legion_9,
        };

        return affectedSeries.Any(series => GetLegionSeries(machineModel, machineType) == series);
    }

    private static bool GetHasSpectrumProfileSwitchingBug(string? machineModel, string machineType)
    {
        if (string.IsNullOrEmpty(machineModel))
            return false;

        var affectedSeries = new[]
        {
            LegionSeries.Legion_5,
            LegionSeries.Legion_Pro_5,
        };

        var affectedModels = new List<string>
        {
            "16IRX10",
            "16IAX10",
            "16IAX10H",
            "15IRX10",
            "15AHP10"
        };

        var isAffectedModel = affectedModels.Any(model => machineModel.Contains(model, StringComparison.OrdinalIgnoreCase));
        var isAffectedSeries = affectedSeries.Any(series => GetLegionSeries(machineModel, machineType) == series);

        return isAffectedModel && isAffectedSeries;
    }

    private static bool GetIsExcludedFromLenovoLighting(BiosVersion? biosVersion, int generation, LegionSeries series)
    {
        if (series == LegionSeries.Legion_7 && generation == 6)
            return true;

        var affectedBiosVersions = new BiosVersion[]
        {
            new("GKCN", 54)
        };

        return affectedBiosVersions.Any(bv => biosVersion?.IsLowerThan(bv) ?? false);
    }

    private static bool GetIsExcludedFromPanelLenovoLighting(string machineType, string model)
    {
        (string machineType, string model)[] excludedModels =
        [
            ("82JH", "15ITH6H"),
            ("82JK", "15ITH6"),
            ("82JM", "17ITH6H"),
            ("82JN", "17ITH6"),
            ("82JU", "15ACH6H"),
            ("82JW", "15ACH6"),
            ("82JY", "17ACH6H"),
            ("82K0", "17ACH6"),
            ("82K1", "15IHU6"),
            ("82K2", "15ACH6"),
            ("82NW", "15ACH6A")
        ];

        return excludedModels.Where(m =>
        {
            var result = machineType.Contains(m.machineType);
            result &= model.Contains(m.model);
            return result;
        }).Any();
    }

    private static bool GetHasAlternativeFullSpectrumLayout(string machineType)
    {
        var machineTypes = new[]
        {
            "83G0", // Gen 9
            "83AG"  // Gen 8
        };
        return machineTypes.Contains(machineType);
    }

    public static bool IsLegion(LegionSeries series)
    {
        return series switch
        {
            LegionSeries.Legion_5 => true,
            LegionSeries.Legion_Pro_5 => true,
            LegionSeries.Lenovo_Slim => true,
            LegionSeries.Legion_Slim_5 => true,
            LegionSeries.Legion_7 => true,
            LegionSeries.Legion_Pro_7 => true,
            LegionSeries.Legion_9 => true,
            LegionSeries.Legion_Go => true,
            LegionSeries.LOQ => true,
            LegionSeries.Legion_Legacy => true,
            _ => false
        };
    }

    public static bool GetIsOverdriverSupported()
    {
        if (_machineInformationLazy.IsValueCreated && _machineInformationLazy.Value.IsCompletedSuccessfully)
        {
            var mi = _machineInformationLazy.Value.GetAwaiter().GetResult();
            return mi.LegionSeries is not (LegionSeries.Legion_7 or LegionSeries.Legion_Pro_7) || mi.Generation < 10;
        }

        return false;
    }
}
