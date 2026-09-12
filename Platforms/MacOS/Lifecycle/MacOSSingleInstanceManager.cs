using UniversalDeviceToolkit.Abstractions.Lifecycle;

namespace UniversalDeviceToolkit.Platform.MacOS.Lifecycle;

/// <summary>
/// macOS implementation of <see cref="ISingleInstanceManager"/>.
/// Uses a file-based lock in <c>/tmp</c> (same pattern as Linux).
/// </summary>
public sealed class MacOSSingleInstanceManager : ISingleInstanceManager
{
    private readonly string _lockFilePath;
    private FileStream? _lockStream;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacOSSingleInstanceManager"/> class.
    /// </summary>
    public MacOSSingleInstanceManager()
    {
        _lockFilePath = Path.Combine(Path.GetTempPath(), "udt-single-instance.lock");
    }

    /// <inheritdoc />
    public bool TryAcquire()
    {
        try
        {
            _lockStream = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            // Write PID for diagnostics
            var pidBytes = System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());
            _lockStream.SetLength(0);
            _lockStream.Write(pidBytes);
            _lockStream.Flush();
            return true;
        }
        catch (IOException)
        {
            _lockStream?.Dispose();
            _lockStream = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool WaitForSignal(TimeSpan timeout)
    {
        // On macOS, cross-instance signaling is not natively supported without IPC.
        // Stub: simply wait and return false (no signal infrastructure).
        Thread.Sleep(Math.Min((int)timeout.TotalMilliseconds, 500));
        return false;
    }

    /// <inheritdoc />
    public void SendSignal()
    {
        // On macOS, cross-instance signaling is not natively supported without IPC.
        // Stub: no-op. A real implementation could use Unix domain sockets.
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lockStream?.Dispose();
        _lockStream = null;

        try
        {
            if (File.Exists(_lockFilePath))
                File.Delete(_lockFilePath);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
