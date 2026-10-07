using System;
using System.IO;
using System.Threading;

namespace UniversalDeviceToolkit.Lib.Network;

internal sealed class NetworkAccelerationUserLease : IDisposable
{
    private static readonly AsyncLocal<NetworkAccelerationUserLease?> ActiveLease = new();
    private readonly FileStream _stream;
    private int _disposed;

    private NetworkAccelerationUserLease(string leasePath, FileStream stream)
    {
        LeasePath = leasePath;
        _stream = stream;
    }

    internal string LeasePath { get; }

    internal static string DefaultPath
    {
        get
        {
            var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(userDirectory))
                throw new InvalidOperationException("The current user's local application data directory is unavailable.");
            return Path.Combine(userDirectory, "UniversalDeviceToolkit", "network-acceleration.lease");
        }
    }

    internal static NetworkAccelerationUserLease Acquire(string? leasePath = null)
    {
        var path = Path.GetFullPath(leasePath ?? DefaultPath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The network lease directory is unavailable.");
        Directory.CreateDirectory(directory);
        return new NetworkAccelerationUserLease(path,
            new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
    }

    internal static IDisposable AcquireOperation(string? leasePath = null)
    {
        var path = Path.GetFullPath(leasePath ?? DefaultPath);
        var active = ActiveLease.Value;
        if (active is not null && Volatile.Read(ref active._disposed) == 0 &&
            string.Equals(active.LeasePath, path, StringComparison.OrdinalIgnoreCase))
            return active.EnterScope();

        var acquired = Acquire(path);
        try
        {
            return new OwnedOperation(acquired, acquired.EnterScope());
        }
        catch
        {
            acquired.Dispose();
            throw;
        }
    }

    internal IDisposable EnterScope()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var previous = ActiveLease.Value;
        ActiveLease.Value = this;
        return new Scope(this, previous);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _stream.Dispose();
    }

    private sealed class Scope(NetworkAccelerationUserLease lease, NetworkAccelerationUserLease? previous) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0 && ReferenceEquals(ActiveLease.Value, lease))
                ActiveLease.Value = previous;
        }
    }

    private sealed class OwnedOperation(NetworkAccelerationUserLease lease, IDisposable scope) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                scope.Dispose();
            }
            finally
            {
                lease.Dispose();
            }
        }
    }
}
