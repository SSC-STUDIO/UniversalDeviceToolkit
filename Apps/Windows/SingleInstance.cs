using System.Runtime.Versioning;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Keeps one desktop session and forwards later launches without polling.</summary>
[SupportedOSPlatform("windows")]
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _registration;

    public bool IsPrimary { get; }

    public SingleInstance(string name)
    {
        _mutex = new Mutex(false, name, out var created);
        IsPrimary = created;
        // AutoReset preserves an early activation until the primary starts listening.
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        if (!IsPrimary) _activation.Set();
    }

    public void Listen(Action activate)
    {
        if (!IsPrimary || _registration != null)
            throw new InvalidOperationException("Only the primary instance may register one activation listener.");
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activation, (_, _) => activate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activation.Dispose();
        _mutex.Dispose();
    }
}
