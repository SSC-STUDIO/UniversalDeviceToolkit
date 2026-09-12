using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Platform.Linux.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;

namespace UniversalDeviceToolkit.Platform.Linux;

/// <summary>
/// Projects DMI identity, power_supply adapter status, and capability probes
/// into the shared snapshot contract. Hardware writes stay opt-in per backend.
/// </summary>
public sealed class LinuxDeviceAdapter : IDeviceAdapter
{
    private readonly IPlatformServices _services;
    private readonly ILinuxFileSystem _fileSystem;
    private readonly IReadOnlyCollection<DevicePackDefinition> _packs;

    public LinuxDeviceAdapter(
        IPlatformServices? services = null,
        IReadOnlyCollection<DevicePackDefinition>? packs = null,
        ILinuxFileSystem? fileSystem = null)
    {
        _services = services ?? new LinuxPlatformServices();
        _packs = packs ?? DevicePackCatalogLoader.Load();
        _fileSystem = fileSystem ?? PhysicalLinuxFileSystem.Instance;
    }

    public string PlatformId => "linux";

    public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identity = LinuxDmiIdentity.Read(_fileSystem);
        var support = DeviceSupportMatcher.Evaluate(identity, _packs);
        var power = LinuxPowerSupplyReader.Read(_fileSystem);
        IReadOnlyList<DeviceCapability> capabilities =
        [
            Capability("gpu-management", _services.SupportsGpuManagement, "Linux GPU backend was detected."),
            Capability("fan-control", _services.SupportsFanControl, "Linux fan backend was detected."),
            Capability("keyboard-backlight", _services.SupportsKeyboardBacklight, "Linux keyboard backlight backend was detected."),
            Capability("battery-management", _services.SupportsBatteryManagement, "Linux battery backend was detected."),
            Capability("display-control", _services.SupportsDisplayControl, "Linux display backend was detected."),
            Capability("power-profile", _services.SupportsPowerProfile, "Linux power profile backend was detected."),
            Capability("read-only-telemetry", _services.SupportsSystemTelemetry, "Linux telemetry probe is available."),
        ];

        return Task.FromResult(new DeviceSnapshot(
            identity,
            support,
            capabilities,
            [],
            power.AdapterStatus,
            "linux-dmi-sysfs"));
    }

    private static DeviceCapability Capability(string id, bool available, string availableReason) =>
        available
            ? new DeviceCapability(id, true, false, "linux-platform-probe", availableReason)
            : DeviceCapability.Unavailable(id, "No verified generic Linux backend was detected.", "linux-platform-probe");
}

internal static class LinuxDmiIdentity
{
    private const string DmiRoot = "/sys/class/dmi/id";

    public static DeviceIdentity Read(ILinuxFileSystem fileSystem)
    {
        var vendor = ReadDmi(fileSystem, "sys_vendor");
        var productName = ReadDmi(fileSystem, "product_name");
        var productVersion = ReadDmi(fileSystem, "product_version");
        var boardVendor = ReadDmi(fileSystem, "board_vendor");
        var boardName = ReadDmi(fileSystem, "board_name");
        var biosVersion = ReadDmi(fileSystem, "bios_version");
        var serial = ReadDmi(fileSystem, "product_serial");
        var sku = ReadDmi(fileSystem, "product_sku");
        var chassis = ReadDmi(fileSystem, "chassis_type");

        var resolvedVendor = FirstPresent(vendor, boardVendor);
        var resolvedProduct = FirstPresent(productName, boardName);
        var model = JoinPresent(" ", productName, productVersion);
        if (string.IsNullOrWhiteSpace(model))
            model = resolvedProduct;

        return new DeviceIdentity(
            "linux",
            RuntimeInformation.OSArchitecture.ToString(),
            resolvedVendor,
            model,
            resolvedProduct,
            biosVersion,
            serial,
            "linux-dmi")
        {
            MachineType = FirstPresent(sku, chassis)
        };
    }

    private static string ReadDmi(ILinuxFileSystem fileSystem, string fileName)
    {
        var raw = fileSystem.ReadText($"{DmiRoot}/{fileName}")?.Trim() ?? string.Empty;
        return IsPlaceholder(raw) ? string.Empty : raw;
    }

    private static bool IsPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("None", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Not Specified", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("System Product Name", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Default string", StringComparison.OrdinalIgnoreCase);

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string JoinPresent(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}
