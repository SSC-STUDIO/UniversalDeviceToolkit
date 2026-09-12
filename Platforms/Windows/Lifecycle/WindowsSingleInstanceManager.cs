using UniversalDeviceToolkit.Abstractions.Lifecycle;

namespace UniversalDeviceToolkit.Platform.Windows.Lifecycle;

public sealed class WindowsSingleInstanceManager : ISingleInstanceManager
{
    private const string MutexName = @"Global\UniversalDeviceToolkit_SingleInstance";
    private const string EventName = @"Global\UniversalDeviceToolkit_SingleInstance_Event";

    private Mutex? _mutex;
    private EventWaitHandle? _eventWaitHandle;
    private bool _disposed;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (createdNew)
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public bool WaitForSignal(TimeSpan timeout)
    {
        _eventWaitHandle ??= new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        return _eventWaitHandle.WaitOne(timeout);
    }

    public void SendSignal()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(EventName);
            evt.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // No instance is listening; ignore.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _eventWaitHandle?.Dispose();
        _mutex?.Dispose();
    }
}
