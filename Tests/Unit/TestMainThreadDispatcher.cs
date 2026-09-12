using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Tests;

public class TestMainThreadDispatcher : IMainThreadDispatcher
{
    public void Dispatch(Action callback) => callback();

    public Task DispatchAsync(Func<Task> callback) => callback();
}
