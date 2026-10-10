using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Abstractions.Hardware;

internal sealed record DeviceSupportStatus(
    string SupportLevel,
    string DevicePackId,
    string DisplayName,
    string[] EnabledFeatures,
    string[] HiddenFeatures,
    string Reason)
{
    public bool IsHardwareControlAvailable =>
        EnabledFeatures.Contains("lenovo-hardware-controls", StringComparer.OrdinalIgnoreCase) &&
        !HiddenFeatures.Contains("lenovo-hardware-controls", StringComparer.OrdinalIgnoreCase);

    public static DeviceSupportStatus From(DeviceSupportInfo support) =>
        new(
            support.SupportLevel,
            support.DevicePackId,
            support.DisplayName,
            support.EnabledFeatures.ToArray(),
            support.HiddenFeatures.ToArray(),
            support.Reason);
}

internal sealed class CrossPlatformDeviceSupportEvaluator
{
    private readonly IReadOnlyCollection<DevicePackDefinition> _packs;

    public CrossPlatformDeviceSupportEvaluator(IReadOnlyCollection<DevicePackDefinition>? packs = null)
    {
        _packs = packs ?? DevicePackCatalogLoader.Load();
    }

    public DeviceSupportStatus Evaluate(HardwareIdentity hardware, bool isWindows)
    {
        var platform = hardware.Source.StartsWith("linux", StringComparison.OrdinalIgnoreCase)
            ? "linux"
            : hardware.Source.StartsWith("macos", StringComparison.OrdinalIgnoreCase)
                ? "macos"
                : isWindows ? "windows" : "unknown";
        var identity = new DeviceIdentity(
            platform,
            RuntimeInformation.OSArchitecture.ToString(),
            hardware.Vendor,
            hardware.Model,
            hardware.ProductName,
            string.Empty,
            hardware.SerialNumber,
            hardware.Source);

        return DeviceSupportStatus.From(
            DeviceSupportMatcher.Evaluate(identity, _packs));
    }
}
