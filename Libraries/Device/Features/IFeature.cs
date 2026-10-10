using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Features;

public interface IFeature<T> where T : struct
{
    Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default);
    Task<T[]> GetAllStatesAsync(CancellationToken cancellationToken = default);
    Task<T> GetStateAsync(CancellationToken cancellationToken = default);
    Task SetStateAsync(T state, CancellationToken cancellationToken = default);
    void InvalidateResolution();
}
