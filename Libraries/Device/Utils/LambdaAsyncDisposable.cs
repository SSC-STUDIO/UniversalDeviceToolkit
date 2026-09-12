using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Utils;

public class LambdaAsyncDisposable(Func<Task> action) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await action().ConfigureAwait(false);
    }
}
