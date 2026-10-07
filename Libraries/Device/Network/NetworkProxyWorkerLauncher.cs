using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Spawns / stops the isolated NetworkProxy worker process.</summary>
public sealed class NetworkProxyWorkerLauncher : IAsyncDisposable
{
    public const string WorkerFileName = "UniversalDeviceToolkit.NetworkProxy.exe";
    public const string HostProjectDirectoryName = "Host";
    public const string WorkerProjectDirectoryName = "NetworkProxy";

    private readonly object _gate = new();
    private Process? _process;
    private string? _sessionToken;
    private string? _pipeName;
    private int _listenPort;

    public bool IsWorkerAlive
    {
        get
        {
            lock (_gate)
                return _process is { HasExited: false };
        }
    }

    public string? SessionToken
    {
        get
        {
            lock (_gate)
                return _sessionToken;
        }
    }

    public string? PipeName
    {
        get
        {
            lock (_gate)
                return _pipeName;
        }
    }

    public int ListenPort
    {
        get
        {
            lock (_gate)
                return _listenPort;
        }
    }

    public static string? ResolveWorkerPath()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in EnumerateWorkerCandidates())
        {
            var fullPath = TryGetFullPath(candidate);
            if (fullPath is null || !seen.Add(fullPath))
                continue;
            if (IsRunnableWorker(fullPath))
                return fullPath;
        }

        return null;
    }

    public static bool IsWorkerAvailable() => ResolveWorkerPath() is not null;

    /// <summary>
    /// Framework-dependent (and flattened self-contained) apphosts need the
    /// <c>.runtimeconfig.json</c> / <c>.deps.json</c> sidecars next to the exe.
    /// </summary>
    internal static bool IsRunnableWorker(string workerPath)
    {
        if (string.IsNullOrWhiteSpace(workerPath) || !File.Exists(workerPath))
            return false;

        var stem = workerPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? workerPath[..^4]
            : workerPath;
        return File.Exists(stem + ".runtimeconfig.json") && File.Exists(stem + ".deps.json");
    }

    internal static IEnumerable<string> EnumerateWorkerCandidates(
        string? programDirectory = null,
        string? baseDirectory = null,
        string? currentDirectory = null)
    {
        programDirectory ??= Folders.Program;
        baseDirectory ??= AppContext.BaseDirectory;
        currentDirectory ??= Environment.CurrentDirectory;

        if (!string.IsNullOrWhiteSpace(programDirectory))
            yield return Path.Combine(programDirectory, WorkerFileName);
        if (!string.IsNullOrWhiteSpace(baseDirectory))
            yield return Path.Combine(baseDirectory, WorkerFileName);
        if (!string.IsNullOrWhiteSpace(currentDirectory))
            yield return Path.Combine(currentDirectory, WorkerFileName);

        foreach (var directory in new[] { programDirectory, baseDirectory, currentDirectory })
        {
            var sibling = TryMapHostDirectoryToNetworkProxyDirectory(directory);
            if (sibling is not null)
                yield return Path.Combine(sibling, WorkerFileName);
        }
    }

    /// <summary>
    /// Maps <c>.../Apps/Host/bin/x64/Debug/{tfm}/win-x64</c>
    /// to the sibling NetworkProxy output with the same configuration layout.
    /// </summary>
    internal static string? TryMapHostDirectoryToNetworkProxyDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var full = TryGetFullPath(directory);
        if (full is null)
            return null;

        var current = full;
        while (!string.IsNullOrEmpty(current))
        {
            var trimmed = current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.GetFileName(trimmed).Equals(HostProjectDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(trimmed);
                if (string.IsNullOrEmpty(parent))
                    return null;

                var relative = Path.GetRelativePath(trimmed, full);
                var proxyRoot = Path.Combine(parent, WorkerProjectDirectoryName);
                var mapped = relative is "." or ""
                    ? proxyRoot
                    : Path.GetFullPath(Path.Combine(proxyRoot, relative));
                return mapped.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            var next = Path.GetDirectoryName(trimmed);
            if (string.IsNullOrEmpty(next) || next.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                break;
            current = next;
        }

        return null;
    }

    private static string? TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }

    internal NetworkProcessIdentity CaptureWorkerIdentity()
    {
        lock (_gate)
        {
            if (_process is not { HasExited: false } process)
                throw new InvalidOperationException("NetworkProxy worker is not running.");
            return NetworkProcessOwnership.Capture(process, includeExecutablePath: true);
        }
    }

    public async Task EnsureStartedAsync(int listenPort, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
                return;
        }

        var workerPath = ResolveWorkerPath()
            ?? throw new FileNotFoundException("NetworkProxy worker executable was not found beside the application.", WorkerFileName);

        var token = NetworkProxySessionToken.Create();
        var pipe = $"{NetworkAccelerationDefaults.DefaultPipeName}-{Guid.NewGuid():N}"[..Math.Min(80, NetworkAccelerationDefaults.DefaultPipeName.Length + 1 + 32)];
        var port = listenPort > 0 ? listenPort : NetworkAccelerationDefaults.DefaultListenPort;

        // Token goes via env (not argv) so it is not visible in process listings / WMI CommandLine.
        // Worker still accepts --token for backward compatibility.
        var startInfo = new ProcessStartInfo
        {
            FileName = workerPath,
            Arguments = $"--pipe {pipe} --port {port}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(workerPath) ?? Folders.Program
        };
        startInfo.Environment[NetworkProxySessionToken.WorkerTokenEnvironmentVariable] = token;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start NetworkProxy worker.");
        }

        lock (_gate)
        {
            _process?.Dispose();
            _process = process;
            _sessionToken = token;
            _pipeName = pipe;
            _listenPort = port;
        }

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Log.Instance.Trace("NetworkProxy: " + args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                Log.Instance.Warning("NetworkProxy: " + args.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Give the IPC server a brief moment to bind the pipe.
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);

        if (process.HasExited)
            throw new InvalidOperationException($"NetworkProxy worker exited early (code {process.ExitCode}).");
    }

    public NetworkProxyIpcClient CreateClient()
    {
        lock (_gate)
        {
            if (_sessionToken is null || _pipeName is null)
                throw new InvalidOperationException("Worker has not been started.");
            return new NetworkProxyIpcClient(_pipeName, _sessionToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        NetworkProxyIpcClient? client = null;

        lock (_gate)
        {
            process = _process;
            if (_sessionToken is not null && _pipeName is not null)
            {
                try { client = new NetworkProxyIpcClient(_pipeName, _sessionToken); }
                catch (Exception ex)
                {
                    Log.Instance.TraceOnce("network-proxy-ipc-client", "Failed to create NetworkProxy IPC client during stop.", ex);
                    client = null;
                }
            }
        }

        if (client is not null)
        {
            try
            {
                await client.StopAsync(cancellationToken).ConfigureAwait(false);
                await client.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Instance.WarningOnce(
                    "network-proxy-graceful-stop",
                    "NetworkProxy graceful stop failed; falling back to process kill.",
                    ex);
            }
        }

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Instance.WarningOnce(
                    "network-proxy-force-kill",
                    "Best-effort NetworkProxy process kill failed during stop.",
                    ex);
            }
            finally
            {
                process.Dispose();
            }
        }

        lock (_gate)
        {
            if (ReferenceEquals(_process, process))
                _process = null;
            _sessionToken = null;
            _pipeName = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
