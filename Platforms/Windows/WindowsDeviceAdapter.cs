using UniversalDeviceToolkit.Abstractions.Hardware;
using CoreAdapter = UniversalDeviceToolkit.Platform.Windows.Core.WindowsDeviceAdapterCore;
using CoreReader = UniversalDeviceToolkit.Platform.Windows.Core.IWindowsWmiReader;

namespace UniversalDeviceToolkit.Platform.Windows;

/// <summary>
/// Windows desktop facade over the portable WMI adapter implementation.
/// </summary>
public sealed class WindowsDeviceAdapter : IDeviceAdapter
{
    private readonly CoreAdapter _inner;

    public WindowsDeviceAdapter(
        IWindowsWmiReader? wmiReader = null,
        IReadOnlyCollection<DevicePackDefinition>? packs = null)
    {
        _inner = new CoreAdapter(wmiReader, packs);
    }

    public string PlatformId => "windows";

    public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) =>
        _inner.ReadSnapshotAsync(cancellationToken);
}

public interface IWindowsWmiReader : CoreReader
{
}
