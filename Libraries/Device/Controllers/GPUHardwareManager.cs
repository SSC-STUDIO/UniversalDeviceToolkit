using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers;

public interface IGPUHardwareManager
{
    Task RestartGPUAsync(string gpuInstanceId);
}

public class GPUHardwareManager : IGPUHardwareManager
{
    public async Task RestartGPUAsync(string gpuInstanceId)
    {
        if (string.IsNullOrWhiteSpace(gpuInstanceId))
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Restarting GPU device: {gpuInstanceId}");

        await CMD.RunAsync("pnputil", $"/restart-device \"{gpuInstanceId}\"").ConfigureAwait(false);
    }
}
