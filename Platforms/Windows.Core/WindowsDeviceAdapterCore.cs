using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.Windows.Core;

/// <summary>
/// Portable assembly boundary for the read-only Windows WMI machine adapter.
/// The WMI path is only executed when the host runtime is Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceAdapterCore : IDeviceAdapter
{
    private readonly IWindowsWmiReader _wmiReader;
    private readonly IReadOnlyCollection<DevicePackDefinition> _packs;

    public WindowsDeviceAdapterCore(
        IWindowsWmiReader? wmiReader = null,
        IReadOnlyCollection<DevicePackDefinition>? packs = null)
    {
        _wmiReader = wmiReader ?? new WindowsWmiReader();
        _packs = packs ?? DevicePackCatalogLoader.Load();
    }

    public string PlatformId => "windows";

    public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // WMI queries are synchronous even though the host contract is async.
        // Keep them off the WPF UI thread so the shell can render before
        // hardware discovery completes.
        return Task.Run(() => ReadSnapshotCore(cancellationToken), cancellationToken);
    }

    private DeviceSnapshot ReadSnapshotCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var computer = First("Win32_ComputerSystem");
        var bios = First("Win32_BIOS");
        var processor = First("Win32_Processor");
        var battery = First("Win32_Battery");
        var video = First("Win32_VideoController");

        var identity = new DeviceIdentity(
            "windows",
            RuntimeInformation.OSArchitecture.ToString(),
            Value(computer, "Manufacturer"),
            Value(computer, "Model"),
            Value(computer, "Model"),
            FirstPresent(Value(bios, "SMBIOSBIOSVersion"), Value(bios, "Version")),
            Value(bios, "SerialNumber"),
            "windows-wmi")
        {
            MachineType = FirstPresent(Value(computer, "SystemSKUNumber"), Value(computer, "SystemFamily")),
        };
        var support = DeviceSupportMatcher.Evaluate(identity, _packs);
        var sensors = ReadSensors(computer, processor, battery, video);
        var capabilities = BuildCapabilities(computer, processor, battery);

        return new DeviceSnapshot(
            identity,
            support,
            capabilities,
            sensors,
            FormatPowerStatus(battery),
            "windows-wmi");
    }

    private IReadOnlyDictionary<string, string?> First(string className) =>
        _wmiReader.Query(className).FirstOrDefault() ?? EmptyRow.Instance;

    private static IReadOnlyList<SensorReading> ReadSensors(
        IReadOnlyDictionary<string, string?> computer,
        IReadOnlyDictionary<string, string?> processor,
        IReadOnlyDictionary<string, string?> battery,
        IReadOnlyDictionary<string, string?> video)
    {
        var readings = new List<SensorReading>();
        if (TryDouble(processor, "LoadPercentage", out var load))
            readings.Add(new SensorReading("CPU Usage", "CPU", load, "%"));
        if (TryDouble(processor, "NumberOfLogicalProcessors", out var logicalProcessors))
            readings.Add(new SensorReading("Logical CPUs", "System", logicalProcessors, "count"));
        if (TryDouble(computer, "TotalPhysicalMemory", out var memoryBytes))
            readings.Add(new SensorReading("Memory Total", "Memory", memoryBytes / 1024d / 1024d / 1024d, "GiB"));
        if (TryDouble(battery, "EstimatedChargeRemaining", out var charge))
            readings.Add(new SensorReading("Battery Charge", "Battery", charge, "%"));
        var gpu = Value(video, "Name");
        if (!string.IsNullOrWhiteSpace(gpu))
            readings.Add(new SensorReading("GPU", "GPU", 1, gpu));

        return readings;
    }

    private static IReadOnlyList<DeviceCapability> BuildCapabilities(
        IReadOnlyDictionary<string, string?> identity,
        IReadOnlyDictionary<string, string?> processor,
        IReadOnlyDictionary<string, string?> battery)
    {
        var identityAvailable = !string.IsNullOrWhiteSpace(Value(identity, "Manufacturer")) ||
                                !string.IsNullOrWhiteSpace(Value(identity, "Model"));
        var telemetryAvailable = processor.Count > 0;
        var batteryAvailable = battery.Count > 0;

        return
        [
            identityAvailable
                ? new DeviceCapability("hardware-identity", true, false, "windows-wmi", "WMI identity data is available.")
                : DeviceCapability.Unavailable("hardware-identity", "WMI returned no computer identity.", "windows-wmi"),
            telemetryAvailable
                ? new DeviceCapability("read-only-telemetry", true, false, "windows-wmi", "WMI processor and memory data is available.")
                : DeviceCapability.Unavailable("read-only-telemetry", "WMI returned no processor data.", "windows-wmi"),
            batteryAvailable
                ? new DeviceCapability("power-diagnostics", true, false, "windows-wmi", "WMI battery data is available.")
                : DeviceCapability.Unavailable("power-diagnostics", "This machine does not expose Win32_Battery data.", "windows-wmi"),
            DeviceCapability.Unavailable("gpu-management", "Generic GPU writes require a verified vendor backend.", "windows-wmi"),
            DeviceCapability.Unavailable("fan-control", "Generic fan writes require a verified vendor backend.", "windows-wmi"),
            DeviceCapability.Unavailable("keyboard-backlight", "Generic keyboard backlight writes require a verified vendor backend.", "windows-wmi"),
        ];
    }

    private static string? FormatPowerStatus(IReadOnlyDictionary<string, string?> battery)
    {
        var status = Value(battery, "BatteryStatus");
        var charge = Value(battery, "EstimatedChargeRemaining");
        if (string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(charge))
            return null;

        return string.Join(", ", new[] { status, string.IsNullOrWhiteSpace(charge) ? null : $"{charge}%" }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string Value(IReadOnlyDictionary<string, string?> row, string key) =>
        row.TryGetValue(key, out var value) ? value?.Trim() ?? string.Empty : string.Empty;

    private static bool TryDouble(IReadOnlyDictionary<string, string?> row, string key, out double value) =>
        double.TryParse(Value(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

public interface IWindowsWmiReader
{
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Query(string className);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsWmiReader : IWindowsWmiReader
{
    public IReadOnlyList<IReadOnlyDictionary<string, string?>> Query(string className)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM {className}");
            using var results = searcher.Get();
            return results
                .Cast<ManagementObject>()
                .Select(ToDictionary)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, string?> ToDictionary(ManagementObject managementObject) =>
        managementObject.Properties
            .Cast<PropertyData>()
            .ToDictionary(property => property.Name, property => property.Value?.ToString(), StringComparer.OrdinalIgnoreCase);
}

internal sealed class EmptyRow : Dictionary<string, string?>
{
    public static EmptyRow Instance { get; } = new();
}
