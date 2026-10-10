using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.Windows.Hardware;

// TODO: 后续阶段将委托给 Lib 中的 NVAPI 实现
public sealed class WindowsGpuBackend : IGpuBackend
{
    public bool IsAvailable => false;
    public string? GetGpuName() => null;
    public int? GetUsagePercent() => null;
    public int? GetTemperatureCelsius() => null;
    public int? GetCurrentClockMhz() => null;
    public int? GetBoostClockMhz() => null;
    public int? GetMemoryUsedMb() => null;
    public int? GetMemoryTotalMb() => null;
}
