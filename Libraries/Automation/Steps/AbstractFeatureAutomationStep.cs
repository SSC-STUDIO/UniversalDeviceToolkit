using System.Management;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public abstract class AbstractFeatureAutomationStep<T>(T state)
    : IAutomationStep<T> where T : struct
{
    private readonly IFeature<T> _feature = IoCContainer.Resolve<IFeature<T>>();

    public T State { get; } = state;

    public virtual Task<bool> IsSupportedAsync() => _feature.IsSupportedAsync();

    public virtual async Task RunAsync(AutomationContext context, AutomationEnvironment environment,
        CancellationToken token)
    {
        if (!await IsSupportedAsync().ConfigureAwait(false))
            return;

        try
        {
            var currentState = await _feature.GetStateAsync().ConfigureAwait(false);
            if (!State.Equals(currentState))
                await _feature.SetStateAsync(State).ConfigureAwait(false);
            MessagingCenter.Publish(new FeatureStateMessage<T>(State));
        }
        catch (ManagementException ex) when (ex.ErrorCode is ManagementStatus.InvalidClass or ManagementStatus.InvalidNamespace)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Skipping {typeof(T).Name} automation step due to unsupported WMI class.", ex);
        }
    }

    public Task<T[]> GetAllStatesAsync() => _feature.GetAllStatesAsync();

    public abstract IAutomationStep DeepCopy();
}
