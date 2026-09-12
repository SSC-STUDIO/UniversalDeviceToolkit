using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.AutoListeners;

public class InstanceStartedEventAutoAutoListener : AbstractAutoListener<InstanceStartedEventAutoAutoListener.ChangedEventArgs>
{
    public class ChangedEventArgs(int processId, string processName) : EventArgs
    {
        public int ProcessId { get; } = processId;
        public string ProcessName { get; } = processName;
    }

    private IDisposable? _disposable;

    protected override async Task StartAsync()
    {
        _disposable = await WMI.Win32.ProcessStartTrace.ListenAsync(Handle).ConfigureAwait(false);
    }


    protected override Task StopAsync()
    {
        _disposable?.Dispose();
        _disposable = null;
        return Task.CompletedTask;
    }

    private void Handle(int processId, string processName) => RaiseChanged(new ChangedEventArgs(processId, processName));
}
