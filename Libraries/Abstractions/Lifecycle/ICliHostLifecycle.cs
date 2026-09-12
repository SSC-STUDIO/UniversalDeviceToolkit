namespace UniversalDeviceToolkit.Abstractions.Lifecycle;

/// <summary>
/// Owns the host-side CLI IPC listener. UI hosts use this contract instead of
/// persisting the CLI preference without updating the running listener.
/// </summary>
public interface ICliHostLifecycle
{
    Task StartStopIfNeededAsync();
    Task StopAsync();
}
