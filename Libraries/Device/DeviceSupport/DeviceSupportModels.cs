using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Text;

namespace UniversalDeviceToolkit.Lib;

public readonly struct BiosVersion(string prefix, int? version)
{
    public string Prefix { get; } = prefix;
    public int? Version { get; } = version;

    public bool IsHigherOrEqualThan(BiosVersion other)
    {
        if (!Prefix.Equals(other.Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Version is null || other.Version is null)
            return true;

        return Version >= other.Version;
    }

    public bool IsLowerThan(BiosVersion other)
    {
        if (!Prefix.Equals(other.Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Version is null || other.Version is null)
            return true;

        return Version < other.Version;
    }

    public override string ToString() => $"{nameof(Prefix)}: {Prefix}, {nameof(Version)}: {Version}";
}

public struct Device(
    string name,
    string description,
    string busReportedDeviceDescription,
    string deviceInstanceId,
    Guid classGuid,
    string className,
    bool isRemovable,
    bool isDisconnected)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string BusReportedDeviceDescription { get; } = busReportedDeviceDescription;
    public string DeviceInstanceId { get; } = deviceInstanceId;
    public Guid ClassGuid { get; } = classGuid;
    public string ClassName { get; } = className;
    public bool IsRemovable { get; } = isRemovable;
    public bool IsDisconnected { get; } = isDisconnected;

    private string? _index;

    public string Index
    {
        get
        {
            _index ??= new StringBuilder()
                .Append(ClassName)
                .Append(ClassGuid)
                .Append(BusReportedDeviceDescription)
                .Append(Description)
                .Append(Name)
                .Append(DeviceInstanceId)
                .ToString();
            return _index;
        }
    }
}

public readonly struct DriverInfo(string deviceId, string hardwareId, Version? version, DateTime? date)
{
    public string DeviceId { get; } = deviceId;
    public string HardwareId { get; } = hardwareId;
    public Version? Version { get; } = version;
    public DateTime? Date { get; } = date;
}

public readonly struct HardwareId(string vendor, string device)
{
    public static readonly HardwareId Empty = new(string.Empty, string.Empty);

    public string Vendor { get; } = vendor ?? string.Empty;
    public string Device { get; } = device ?? string.Empty;

    #region Equality

    public override bool Equals(object? obj)
    {
        if (obj is not HardwareId other)
            return false;

        if (!string.Equals(Vendor, other.Vendor, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(Device, other.Device, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(Vendor ?? string.Empty),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Device ?? string.Empty));

    public static bool operator ==(HardwareId left, HardwareId right) => left.Equals(right);

    public static bool operator !=(HardwareId left, HardwareId right) => !left.Equals(right);

    #endregion
}

public readonly struct MachineInformation
{
    public readonly struct FeatureData(FeatureData.SourceType sourceType, IEnumerable<CapabilityID> capabilities)
    {
        public static readonly FeatureData Unknown = new(SourceType.Unknown);

        public enum SourceType
        {
            Unknown,
            Flags,
            CapabilityData
        }

        private readonly HashSet<CapabilityID> _capabilities = [.. capabilities];

        public SourceType Source { get; } = sourceType;

        public IEnumerable<CapabilityID> All => _capabilities.Order().AsEnumerable();

        public FeatureData(SourceType sourceType) : this(sourceType, []) { }

        public bool this[CapabilityID key]
        {
            get => _capabilities.Contains(key);
            init
            {
                if (value)
                    _capabilities.Add(key);
                else
                    _capabilities.Remove(key);
            }
        }
    }

    public readonly struct PropertyData
    {
        public bool SupportsGodMode => SupportsGodModeV1 || SupportsGodModeV2 || SupportsGodModeV3 || SupportsGodModeV4;

        public (bool status, bool connectivity) SupportsAlwaysOnAc { get; init; }
        public bool SupportsExtremeMode { get; init; }
        public bool SupportsGodModeV1 { get; init; }
        public bool SupportsGodModeV2 { get; init; }
        public bool SupportsGodModeV3 { get; init; }
        public bool SupportsGodModeV4 { get; init; }
        public bool SupportsGSync { get; init; }
        public bool SupportsIGPUMode { get; init; }
        public bool SupportsAIMode { get; init; }
        public bool SupportBootLogoChange { get; init; }
        public bool SupportsBootLogoChange => SupportBootLogoChange;
        public bool SupportsITSMode { get; init; }
        public bool HasQuietToPerformanceModeSwitchingBug { get; init; }
        public bool HasGodModeToOtherModeSwitchingBug { get; init; }
        public bool HasReapplyParameterIssue { get; init; }
        public bool HasSpectrumProfileSwitchingBug { get; init; }
        public bool IsExcludedFromLenovoLighting { get; init; }
        public bool IsExcludedFromPanelLogoLenovoLighting { get; init; }
        public bool HasAlternativeFullSpectrumLayout { get; init; }
        public bool IsAmdDevice { get; init; }
        public bool IsChineseModel { get; init; }
    }

    public int Generation { get; init; }
    public LegionSeries LegionSeries { get; init; }
    public string Vendor { get; init; }
    public string MachineType { get; init; }
    public string Model { get; init; }
    public string SerialNumber { get; init; }
    public BiosVersion? BiosVersion { get; init; }
    public string? BiosVersionRaw { get; init; }
    public PowerModeState[] SupportedPowerModes { get; init; }
    public int SmartFanVersion { get; init; }
    public int LegionZoneVersion { get; init; }
    public FeatureData Features { get; init; }
    public PropertyData Properties { get; init; }
    public HardwareInventory Hardware { get; init; }
}

public readonly struct WarrantyInfo(DateTime? start, DateTime? end, Uri? link)
{
    public DateTime? Start { get; } = start;
    public DateTime? End { get; } = end;
    public Uri? Link { get; } = link;
}

public readonly struct HidDeviceConfig(
    ushort vendorId,
    ushort productId,
    ushort usagePage,
    ushort usage,
    string displayName)
{
    public ushort VendorId { get; } = vendorId;
    public ushort ProductId { get; } = productId;
    public ushort UsagePage { get; } = usagePage;
    public ushort Usage { get; } = usage;
    public string DisplayName { get; } = displayName;
}
