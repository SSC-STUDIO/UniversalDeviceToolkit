using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.Windows.Hardware;

// TODO: 后续阶段将委托给 Lib 中的 LibreHardwareMonitor 实现
public sealed class WindowsSensorBackend : ISensorBackend
{
    public bool IsAvailable => false;
    public IReadOnlyList<SensorReading> GetReadings() => [];
}
