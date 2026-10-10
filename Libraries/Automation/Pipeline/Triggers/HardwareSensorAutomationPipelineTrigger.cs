using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Automation.Resources;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public enum HardwareSensorMetric { CpuTemperature, CpuUsage, CpuPower, GpuTemperature, GpuUsage, GpuPower }
public enum HardwareSensorComparison { GreaterThanOrEqual, LessThanOrEqual }

[method: JsonConstructor]
public sealed class HardwareSensorAutomationPipelineTrigger(
    HardwareSensorMetric metric,
    HardwareSensorComparison comparison,
    float threshold,
    TimeSpan duration,
    TimeSpan cooldown) : IAutomationPipelineTrigger
{
    private DateTimeOffset? _matchingSince;
    private DateTimeOffset _lastMatchedAt = DateTimeOffset.MinValue;

    public HardwareSensorMetric Metric { get; } = metric;
    public HardwareSensorComparison Comparison { get; } = comparison;
    public float Threshold { get; } = threshold;
    public TimeSpan Duration { get; } = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    public TimeSpan Cooldown { get; } = cooldown < TimeSpan.Zero ? TimeSpan.Zero : cooldown;
    public string DisplayName => string.Format(Resource.HardwareSensorAutomationPipelineTrigger_DisplayName_Format, Metric, Comparison, Threshold);

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent) => IsMatchingState();

    public async Task<bool> IsMatchingState()
    {
        var value = await ReadCachedValueAsync().ConfigureAwait(false);
        if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
        {
            _matchingSince = null;
            return false;
        }

        var matches = Comparison == HardwareSensorComparison.GreaterThanOrEqual ? value >= Threshold : value <= Threshold;
        if (!matches)
        {
            _matchingSince = null;
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        _matchingSince ??= now;
        if (now - _matchingSince.Value < Duration || now - _lastMatchedAt < Cooldown)
            return false;

        _lastMatchedAt = now;
        return true;
    }

    private Task<float> ReadCachedValueAsync()
    {
        var sensors = IoCContainer.Resolve<SensorsGroupController>();
        return Metric switch
        {
            HardwareSensorMetric.CpuTemperature => sensors.GetCpuTemperatureAsync(),
            HardwareSensorMetric.CpuUsage => sensors.GetCpuUsageAsync(),
            HardwareSensorMetric.CpuPower => sensors.GetCpuPowerAsync(),
            HardwareSensorMetric.GpuTemperature => sensors.GetGpuTemperatureAsync(),
            HardwareSensorMetric.GpuUsage => sensors.GetGpuUsageAsync(),
            HardwareSensorMetric.GpuPower => sensors.GetGpuPowerAsync(),
            _ => Task.FromResult(-1f)
        };
    }

    public void UpdateEnvironment(AutomationEnvironment environment) { }
    public IAutomationPipelineTrigger DeepCopy() => new HardwareSensorAutomationPipelineTrigger(Metric, Comparison, Threshold, Duration, Cooldown);

    public HardwareSensorAutomationPipelineTrigger DeepCopy(
        HardwareSensorMetric metric,
        HardwareSensorComparison comparison,
        float threshold,
        TimeSpan duration,
        TimeSpan cooldown) =>
        new(metric, comparison, threshold, duration, cooldown);
}