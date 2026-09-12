using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Automation.Resources;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public enum BatteryPercentageComparison
{
    AboveOrEqual,
    BelowOrEqual
}

public enum BatteryChargeFilter
{
    Any,
    Charging,
    Discharging
}

[method: JsonConstructor]
public sealed class BatteryPercentageAutomationPipelineTrigger(
    BatteryPercentageComparison comparison,
    int threshold,
    TimeSpan duration,
    TimeSpan cooldown,
    BatteryChargeFilter chargeFilter) : IAutomationPipelineTrigger
{
    private DateTimeOffset? _matchingSince;
    private DateTimeOffset _lastMatchedAt = DateTimeOffset.MinValue;

    public BatteryPercentageComparison Comparison { get; } = comparison;
    public int Threshold { get; } = Math.Clamp(threshold, 0, 100);
    public TimeSpan Duration { get; } = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    public TimeSpan Cooldown { get; } = cooldown < TimeSpan.Zero ? TimeSpan.Zero : cooldown;
    public BatteryChargeFilter ChargeFilter { get; } = chargeFilter;

    public string DisplayName => string.Format(Resource.BatteryPercentageAutomationPipelineTrigger_DisplayName_Format, Comparison, Threshold, ChargeFilter);

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent) => IsMatchingState();

    public Task<bool> IsMatchingState()
    {
        int percentage;
        bool isCharging;
        try
        {
            if (!Battery.IsBatteryMonitoringSupported())
            {
                _matchingSince = null;
                return Task.FromResult(false);
            }

            var info = Battery.GetBatteryInformation();
            percentage = info.BatteryPercentage;
            isCharging = info.IsCharging;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Battery percentage trigger could not read battery data.", ex);

            _matchingSince = null;
            return Task.FromResult(false);
        }

        if (percentage is < 0 or > 100)
        {
            _matchingSince = null;
            return Task.FromResult(false);
        }

        if (ChargeFilter == BatteryChargeFilter.Charging && !isCharging)
        {
            _matchingSince = null;
            return Task.FromResult(false);
        }

        if (ChargeFilter == BatteryChargeFilter.Discharging && isCharging)
        {
            _matchingSince = null;
            return Task.FromResult(false);
        }

        var matches = Comparison == BatteryPercentageComparison.AboveOrEqual
            ? percentage >= Threshold
            : percentage <= Threshold;

        if (!matches)
        {
            _matchingSince = null;
            return Task.FromResult(false);
        }

        var now = DateTimeOffset.UtcNow;
        _matchingSince ??= now;
        if (now - _matchingSince.Value < Duration || now - _lastMatchedAt < Cooldown)
            return Task.FromResult(false);

        _lastMatchedAt = now;
        return Task.FromResult(true);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) { }

    public IAutomationPipelineTrigger DeepCopy() =>
        new BatteryPercentageAutomationPipelineTrigger(Comparison, Threshold, Duration, Cooldown, ChargeFilter);

    public BatteryPercentageAutomationPipelineTrigger DeepCopy(
        BatteryPercentageComparison comparison,
        int threshold,
        TimeSpan duration,
        TimeSpan cooldown,
        BatteryChargeFilter chargeFilter) =>
        new(comparison, threshold, duration, cooldown, chargeFilter);
}
