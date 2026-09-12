using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Utils;

public interface IMainThreadDispatcher
{
    void Dispatch(Action callback);

    Task DispatchAsync(Func<Task> callback);
}
