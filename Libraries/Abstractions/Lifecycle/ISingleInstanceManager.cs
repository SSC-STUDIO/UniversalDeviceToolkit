namespace UniversalDeviceToolkit.Abstractions.Lifecycle;

/// <summary>
/// Manages single-instance enforcement for the application,
/// typically backed by a named mutex or platform-equivalent primitive.
/// </summary>
public interface ISingleInstanceManager : IDisposable
{
    /// <summary>
    /// Attempts to acquire the single-instance lock.
    /// </summary>
    /// <returns><see langword="true"/> if the lock was acquired (this is the first instance); otherwise <see langword="false"/>.</returns>
    bool TryAcquire();

    /// <summary>
    /// Blocks until a signal is received from another instance or the timeout elapses.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the signal.</param>
    /// <returns><see langword="true"/> if the signal was received; otherwise <see langword="false"/>.</returns>
    bool WaitForSignal(TimeSpan timeout);

    /// <summary>
    /// Sends a signal to the first instance, typically requesting it to restore its window.
    /// </summary>
    void SendSignal();
}
