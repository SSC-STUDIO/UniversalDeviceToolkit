using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Utils;

public class DefaultDelayProvider : IDelayProvider
{
    public Task Delay(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token); // Updated delay method
}
