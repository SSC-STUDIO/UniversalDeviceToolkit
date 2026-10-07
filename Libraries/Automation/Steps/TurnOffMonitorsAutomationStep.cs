using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Listeners;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public class TurnOffMonitorsAutomationStep : IAutomationStep
{
    private readonly Lazy<NativeWindowsMessageListener> _nativeWindowsMessageListenerLazy = new(AutomationServiceResolver.ResolveRequired<NativeWindowsMessageListener>);
    private NativeWindowsMessageListener _nativeWindowsMessageListener => _nativeWindowsMessageListenerLazy.Value;

    public Task<bool> IsSupportedAsync() => Task.FromResult(IoCContainer.TryResolve<NativeWindowsMessageListener>() is not null);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => _nativeWindowsMessageListener.TurnOffMonitorAsync();

    public IAutomationStep DeepCopy() => new TurnOffMonitorsAutomationStep();
}
