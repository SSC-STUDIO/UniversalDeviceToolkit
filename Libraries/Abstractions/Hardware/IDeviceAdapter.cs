using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Reads platform-specific machine information without making hardware writes.
/// Vendor control backends remain separate and must explicitly opt into writes.
/// </summary>
public interface IDeviceAdapter
{
    string PlatformId { get; }

    Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default);
}
