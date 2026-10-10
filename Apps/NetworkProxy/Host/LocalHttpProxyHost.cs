using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UniversalDeviceToolkit.Lib.Network;

namespace UniversalDeviceToolkit.NetworkProxy.Host;

/// <summary>
/// Localhost-only HTTP proxy with CONNECT tunneling (no MITM / no TLS interception).
/// Binds exclusively to 127.0.0.1 / ::1.
/// CONNECT/HTTP to hosts outside the domain allowlist return 403.
/// Empty allowlist denies all destinations (fail closed until rules are pushed).
/// </summary>
public sealed class LocalHttpProxyHost : INetworkProxyHost, INetworkProxyTrafficSource
{
    private const int HeaderBufferSize = 64 * 1024;
    private static readonly byte[] HeaderDelimiter = "\r\n\r\n"u8.ToArray();
    private static readonly byte[] ConnectOk =
        "HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray();
    private static readonly byte[] BadRequest =
        "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray();
    private static readonly byte[] Forbidden =
        "HTTP/1.1 403 Forbidden\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray();
    private static readonly byte[] BadGateway =
        "HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray();

    private readonly object _gate = new();
    private TcpListener? _listenerV4;
    private TcpListener? _listenerV6;
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptLoopV4;
    private Task? _acceptLoopV6;
    private int _listenPort;
    private int _activeSessions;
    private long _bytesUploaded;
    private long _bytesDownloaded;
    private long _totalConnections;
    private long _nextConnectionId;
    private readonly ConcurrentDictionary<long, ConnectionTelemetry> _activeConnectionTelemetry = new();
    private readonly ConcurrentQueue<ConnectionTelemetry> _recentConnectionTelemetry = new();
    private readonly ConcurrentDictionary<string, DestinationTelemetry> _destinationTelemetry = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Task, byte> _sessionTasks = new();
    private const int RecentConnectionLimit = 80;
    // Empty = deny all until SetDomainAllowlist receives rules (fail closed).
    private string[] _domainAllowlist = [];

    public LocalHttpProxyHost(int listenPort)
    {
        _listenPort = listenPort > 0 ? listenPort : NetworkAccelerationDefaults.DefaultListenPort;
    }

    public bool IsRunning { get; private set; }

    public long BytesUploaded => Interlocked.Read(ref _bytesUploaded);

    public long BytesDownloaded => Interlocked.Read(ref _bytesDownloaded);

    public int ActiveConnections => Math.Max(0, Volatile.Read(ref _activeSessions));

    public long TotalConnections => Interlocked.Read(ref _totalConnections);

    public IReadOnlyList<NetworkProxyConnectionSnapshot> GetConnectionSnapshots(int maxItems = 40)
    {
        var limit = Math.Clamp(maxItems, 1, RecentConnectionLimit);
        return _activeConnectionTelemetry.Values
            .Concat(_recentConnectionTelemetry)
            .GroupBy(connection => connection.Id)
            .Select(group => group.First().ToSnapshot())
            .OrderByDescending(connection => connection.StartedAtUtc)
            .Take(limit)
            .ToArray();
    }

    public IReadOnlyList<NetworkProxyDestinationSnapshot> GetDestinationSnapshots(int maxItems = 40)
    {
        var limit = Math.Clamp(maxItems, 1, 200);
        return _destinationTelemetry.Values
            .Select(destination => destination.ToSnapshot())
            .OrderByDescending(destination => destination.LastUpdatedAtUtc)
            .Take(limit)
            .ToArray();
    }

    public int ListenPort
    {
        get
        {
            lock (_gate)
                return _listenPort;
        }
    }

    public string StatusSummary
    {
        get
        {
            lock (_gate)
            {
                return IsRunning
                    ? $"running loopback:{_listenPort} sessions={_activeSessions} allowlist={_domainAllowlist.Length}"
                    : "stopped (default off)";
            }
        }
    }

    /// <inheritdoc />
    public void SetDomainAllowlist(IReadOnlyList<string>? domains)
    {
        var normalized = (domains ?? Array.Empty<string>())
            .Where(static d => !string.IsNullOrWhiteSpace(d))
            .Select(DomainMatcher.Normalize)
            .Where(static d => d.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        lock (_gate)
            _domainAllowlist = normalized;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (IsRunning)
                return Task.CompletedTask;

            // Bind loopback only — never 0.0.0.0 / ::.
            _listenerV4 = new TcpListener(IPAddress.Loopback, _listenPort);
            _listenerV4.Start();
            _listenPort = ((IPEndPoint)_listenerV4.LocalEndpoint).Port;

            try
            {
                _listenerV6 = new TcpListener(IPAddress.IPv6Loopback, _listenPort);
                _listenerV6.Start();
            }
            catch (SocketException)
            {
                _listenerV6 = null;
            }

            _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _acceptLoopV4 = AcceptLoopAsync(_listenerV4, _acceptCts.Token);
            _acceptLoopV6 = _listenerV6 is null
                ? Task.CompletedTask
                : AcceptLoopAsync(_listenerV6, _acceptCts.Token);
            IsRunning = true;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? loopV4;
        Task? loopV6;
        CancellationTokenSource? cts;
        TcpListener? v4;
        TcpListener? v6;

        lock (_gate)
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            loopV4 = _acceptLoopV4;
            loopV6 = _acceptLoopV6;
            cts = _acceptCts;
            v4 = _listenerV4;
            v6 = _listenerV6;
            _acceptLoopV4 = null;
            _acceptLoopV6 = null;
            _acceptCts = null;
            _listenerV4 = null;
            _listenerV6 = null;
        }

        try { cts?.Cancel(); }
        catch (ObjectDisposedException) { /* already disposed */ }

        try { v4?.Stop(); }
        catch (ObjectDisposedException) { /* already disposed */ }
        catch (SocketException ex)
        {
            Debug.WriteLine($"NetworkProxy: IPv4 listener stop: {ex.SocketErrorCode}");
        }

        try { v6?.Stop(); }
        catch (ObjectDisposedException) { /* already disposed */ }
        catch (SocketException ex)
        {
            Debug.WriteLine($"NetworkProxy: IPv6 listener stop: {ex.SocketErrorCode}");
        }

        if (loopV4 is not null)
        {
            try { await loopV4.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on stop */ }
            catch (ObjectDisposedException) { /* expected on stop */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"NetworkProxy: accept loop V4 exit: {ex.GetType().Name}");
            }
        }

        if (loopV6 is not null)
        {
            try { await loopV6.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on stop */ }
            catch (ObjectDisposedException) { /* expected on stop */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"NetworkProxy: accept loop V6 exit: {ex.GetType().Name}");
            }
        }

        var sessions = _sessionTasks.Keys.ToArray();
        if (sessions.Length > 0)
        {
            try
            {
                await Task.WhenAll(sessions).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Debug.WriteLine("NetworkProxy: timed out waiting for client sessions to stop.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NetworkProxy: client session stop: {ex.GetType().Name}");
            }
        }

        cts?.Dispose();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                var session = HandleClientAsync(client, cancellationToken);
                _sessionTasks[session] = 0;
                _ = session.ContinueWith(
                    completed => _sessionTasks.TryRemove(completed, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                client = null;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeSessions);
        Interlocked.Increment(ref _totalConnections);
        var telemetry = new ConnectionTelemetry(Interlocked.Increment(ref _nextConnectionId));
        _activeConnectionTelemetry[telemetry.Id] = telemetry;
        try
        {
            using (client)
            {
                client.NoDelay = true;
                await using var clientStream = client.GetStream();
                await ProcessSessionAsync(clientStream, telemetry, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Host stop / client abort — expected.
        }
        catch (IOException ex)
        {
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: session IO: {ex.GetType().Name}: {ex.Message}");
        }
        catch (SocketException ex)
        {
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: session socket: {ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            // Per-connection failures must not tear down the host.
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: session error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            CompleteTelemetry(telemetry, cancellationToken.IsCancellationRequested ? "stopped" : "completed");
            Interlocked.Decrement(ref _activeSessions);
        }
    }

    private bool IsHostAllowed(string host)
    {
        string[] allowlist;
        lock (_gate)
            allowlist = _domainAllowlist;

        return DomainMatcher.IsAllowed(host, allowlist);
    }

    private async Task ProcessSessionAsync(
        NetworkStream clientStream,
        ConnectionTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(HeaderBufferSize);
        try
        {
            var (headerLength, totalRead) = await ReadHeadersAsync(clientStream, buffer, cancellationToken)
                .ConfigureAwait(false);
            if (headerLength <= 0)
                return;

            if (!TryParseRequest(buffer.AsSpan(0, headerLength), out var method, out var target, out var hostHeader))
            {
                telemetry.Fail("Invalid proxy request");
                await clientStream.WriteAsync(BadRequest, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                await HandleConnectAsync(clientStream, target, telemetry, cancellationToken).ConfigureAwait(false);
                return;
            }

            var prelude = totalRead > headerLength
                ? buffer.AsMemory(headerLength, totalRead - headerLength)
                : ReadOnlyMemory<byte>.Empty;

            await HandleHttpAsync(clientStream, buffer.AsMemory(0, headerLength), prelude, method, target, hostHeader, telemetry, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task HandleConnectAsync(
        NetworkStream clientStream,
        string target,
        ConnectionTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        if (!TrySplitHostPort(target, defaultPort: 443, out var host, out var port))
        {
            telemetry.Fail("Invalid CONNECT target");
            await clientStream.WriteAsync(BadRequest, cancellationToken).ConfigureAwait(false);
            return;
        }

        telemetry.BindDestination(host, port, "CONNECT", GetOrCreateDestination(host, port));

        if (!IsHostAllowed(host))
        {
            telemetry.SetState("blocked", "Blocked by domain allowlist");
            Debug.WriteLine($"NetworkProxy: CONNECT denied host '{host}' (allowlist)");
            await clientStream.WriteAsync(Forbidden, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var remote = new TcpClient();
        var connectTimer = Stopwatch.StartNew();
        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(15));
            await remote.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
            telemetry.SetState(
                cancellationToken.IsCancellationRequested ? "stopped" : "failed",
                "Remote connection canceled");
            await clientStream.WriteAsync(BadGateway, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (SocketException ex)
        {
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: CONNECT {host}:{port} socket {ex.SocketErrorCode}");
            await clientStream.WriteAsync(BadGateway, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (IOException ex)
        {
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: CONNECT {host}:{port} IO {ex.Message}");
            await clientStream.WriteAsync(BadGateway, cancellationToken).ConfigureAwait(false);
            return;
        }

        remote.NoDelay = true;
        await clientStream.WriteAsync(ConnectOk, cancellationToken).ConfigureAwait(false);
        await using var remoteStream = remote.GetStream();
        await RelayBidirectionalAsync(clientStream, remoteStream, telemetry, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleHttpAsync(
        NetworkStream clientStream,
        ReadOnlyMemory<byte> headerBytes,
        ReadOnlyMemory<byte> bodyPrelude,
        string method,
        string target,
        string? hostHeader,
        ConnectionTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        if (!TryResolveOrigin(target, hostHeader, out var host, out var port, out var originForm))
        {
            telemetry.Fail("Invalid HTTP target");
            await clientStream.WriteAsync(BadRequest, cancellationToken).ConfigureAwait(false);
            return;
        }

        telemetry.BindDestination(host, port, "HTTP", GetOrCreateDestination(host, port));

        if (!IsHostAllowed(host))
        {
            telemetry.SetState("blocked", "Blocked by domain allowlist");
            Debug.WriteLine($"NetworkProxy: HTTP denied host '{host}' (allowlist)");
            await clientStream.WriteAsync(Forbidden, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var remote = new TcpClient();
        var connectTimer = Stopwatch.StartNew();
        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(15));
            await remote.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
            telemetry.SetState(
                cancellationToken.IsCancellationRequested ? "stopped" : "failed",
                "Remote connection canceled");
            await clientStream.WriteAsync(BadGateway, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (SocketException ex)
        {
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: HTTP {host}:{port} socket {ex.SocketErrorCode}");
            await clientStream.WriteAsync(BadGateway, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (IOException ex)
        {
            connectTimer.Stop();
            telemetry.SetConnectLatency(connectTimer.ElapsedMilliseconds);
            telemetry.Fail(ex.Message);
            Debug.WriteLine($"NetworkProxy: HTTP {host}:{port} IO {ex.Message}");
            await clientStream.WriteAsync(BadGateway, cancellationToken).ConfigureAwait(false);
            return;
        }

        remote.NoDelay = true;
        await using var remoteStream = remote.GetStream();

        var rewritten = RewriteRequestLine(headerBytes.Span, method, originForm);
        await remoteStream.WriteAsync(rewritten, cancellationToken).ConfigureAwait(false);
        AddUploaded(telemetry, rewritten.Length);
        if (!bodyPrelude.IsEmpty)
        {
            await remoteStream.WriteAsync(bodyPrelude, cancellationToken).ConfigureAwait(false);
            AddUploaded(telemetry, bodyPrelude.Length);
        }

        await RelayBidirectionalAsync(clientStream, remoteStream, telemetry, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int HeaderLength, int TotalRead)> ReadHeadersAsync(
        NetworkStream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled, buffer.Length - filled), cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0)
                return filled <= 0 ? (0, 0) : (-1, filled);

            filled += read;
            var index = buffer.AsSpan(0, filled).IndexOf(HeaderDelimiter);
            if (index >= 0)
                return (index + HeaderDelimiter.Length, filled);
        }

        return (-1, filled);
    }

    internal static bool TryParseRequest(ReadOnlySpan<byte> headers, out string method, out string target, out string? hostHeader)
    {
        method = string.Empty;
        target = string.Empty;
        hostHeader = null;

        var text = Encoding.ASCII.GetString(headers);
        var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (lineEnd <= 0)
            return false;

        var requestLine = text[..lineEnd];
        var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        method = parts[0];
        target = parts[1];

        const string hostPrefix = "\r\nHost:";
        var hostIndex = text.IndexOf(hostPrefix, StringComparison.OrdinalIgnoreCase);
        if (hostIndex >= 0)
        {
            var valueStart = hostIndex + hostPrefix.Length;
            var valueEnd = text.IndexOf("\r\n", valueStart, StringComparison.Ordinal);
            if (valueEnd < 0)
                valueEnd = text.Length;
            hostHeader = text[valueStart..valueEnd].Trim();
        }

        return !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(target);
    }

    internal static bool TrySplitHostPort(string authority, int defaultPort, out string host, out int port)
    {
        host = string.Empty;
        port = defaultPort;

        if (string.IsNullOrWhiteSpace(authority))
            return false;

        authority = authority.Trim();
        if (authority.StartsWith('[') && authority.Contains(']', StringComparison.Ordinal))
        {
            var close = authority.IndexOf(']');
            host = authority[1..close];
            if (close + 1 < authority.Length && authority[close + 1] == ':')
            {
                if (!int.TryParse(authority[(close + 2)..], NumberStyles.None, CultureInfo.InvariantCulture, out port))
                    return false;
            }

            return !string.IsNullOrWhiteSpace(host) && port is > 0 and <= 65535;
        }

        var colon = authority.LastIndexOf(':');
        if (colon > 0 && authority.IndexOf(':') == colon)
        {
            host = authority[..colon];
            if (!int.TryParse(authority[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port))
                return false;
        }
        else
        {
            host = authority;
        }

        return !string.IsNullOrWhiteSpace(host) && port is > 0 and <= 65535;
    }

    internal static bool TryResolveOrigin(
        string target,
        string? hostHeader,
        out string host,
        out int port,
        out string originForm)
    {
        host = string.Empty;
        port = 80;
        originForm = "/";

        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
                return false;

            host = uri.Host;
            port = uri.IsDefaultPort ? (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80) : uri.Port;
            originForm = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            return !string.IsNullOrWhiteSpace(host);
        }

        originForm = string.IsNullOrWhiteSpace(target) ? "/" : target;
        if (string.IsNullOrWhiteSpace(hostHeader))
            return false;

        return TrySplitHostPort(hostHeader, 80, out host, out port);
    }

    private static byte[] RewriteRequestLine(ReadOnlySpan<byte> headers, string method, string originForm)
    {
        var text = Encoding.ASCII.GetString(headers);
        var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (lineEnd <= 0)
            return headers.ToArray();

        var rest = text[lineEnd..];
        var version = "HTTP/1.1";
        var first = text[..lineEnd].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (first.Length >= 3)
            version = first[2];

        var rewritten = $"{method} {originForm} {version}{rest}";
        return Encoding.ASCII.GetBytes(rewritten);
    }

    private async Task RelayBidirectionalAsync(
        NetworkStream left,
        NetworkStream right,
        ConnectionTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var forward = CopyAsync(left, right, linked.Token, bytes => AddUploaded(telemetry, bytes));
        var backward = CopyAsync(right, left, linked.Token, bytes => AddDownloaded(telemetry, bytes));
        var completed = await Task.WhenAny(forward, backward).ConfigureAwait(false);
        linked.Cancel();
        try { await Task.WhenAll(forward, backward).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* peer close / cancel expected */ }
        catch (IOException) { /* peer close expected */ }
        catch (SocketException) { /* peer close expected */ }
        _ = completed;
    }

    private static async Task CopyAsync(
        NetworkStream source,
        NetworkStream destination,
        CancellationToken cancellationToken,
        Action<int> onBytesForwarded)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                    break;
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                onBytesForwarded(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private DestinationTelemetry GetOrCreateDestination(string host, int port)
    {
        var normalizedHost = NormalizeDestinationHost(host);
        var key = $"{normalizedHost}|{port}";
        return _destinationTelemetry.GetOrAdd(
            key,
            _ => new DestinationTelemetry(normalizedHost, port));
    }

    private void AddUploaded(ConnectionTelemetry telemetry, int bytes)
    {
        Interlocked.Add(ref _bytesUploaded, bytes);
        telemetry.AddUploaded(bytes);
    }

    private void AddDownloaded(ConnectionTelemetry telemetry, int bytes)
    {
        Interlocked.Add(ref _bytesDownloaded, bytes);
        telemetry.AddDownloaded(bytes);
    }

    private void CompleteTelemetry(ConnectionTelemetry telemetry, string fallbackState)
    {
        if (!telemetry.TryComplete(fallbackState, out var destination, out var state))
            return;

        _activeConnectionTelemetry.TryRemove(telemetry.Id, out _);
        destination?.Complete(state);
        _recentConnectionTelemetry.Enqueue(telemetry);
        while (_recentConnectionTelemetry.Count > RecentConnectionLimit &&
               _recentConnectionTelemetry.TryDequeue(out _))
        {
        }
    }

    private static string NormalizeDestinationHost(string host) =>
        host.Trim().TrimEnd('.').ToLowerInvariant();

    private sealed class ConnectionTelemetry
    {
        private readonly object _gate = new();
        private string _host = string.Empty;
        private int _port;
        private string _protocol = "Unknown";
        private DateTime? _completedAtUtc;
        private long _bytesUploaded;
        private long _bytesDownloaded;
        private long? _connectLatencyMs;
        private string _state = "active";
        private string? _error;
        private DestinationTelemetry? _destination;
        private bool _destinationBound;

        public ConnectionTelemetry(long id)
        {
            Id = id;
            StartedAtUtc = DateTime.UtcNow;
        }

        public long Id { get; }

        public DateTime StartedAtUtc { get; }

        public void BindDestination(
            string host,
            int port,
            string protocol,
            DestinationTelemetry destination)
        {
            lock (_gate)
            {
                if (_destinationBound)
                    return;

                _host = NormalizeDestinationHost(host);
                _port = port;
                _protocol = protocol;
                _destination = destination;
                _destinationBound = true;
            }

            destination.Begin();
        }

        public void SetConnectLatency(long latencyMs)
        {
            lock (_gate)
                _connectLatencyMs = Math.Max(0, latencyMs);
            _destination?.SetConnectLatency(latencyMs);
        }

        public void AddUploaded(int bytes)
        {
            if (bytes <= 0)
                return;

            lock (_gate)
                _bytesUploaded += bytes;
            _destination?.AddUploaded(bytes);
        }

        public void AddDownloaded(int bytes)
        {
            if (bytes <= 0)
                return;

            lock (_gate)
                _bytesDownloaded += bytes;
            _destination?.AddDownloaded(bytes);
        }

        public void Fail(string error)
        {
            SetState("failed", error);
        }

        public void SetState(string state, string? error = null)
        {
            lock (_gate)
            {
                if (_completedAtUtc is not null)
                    return;

                _state = string.IsNullOrWhiteSpace(state) ? "unknown" : state;
                if (!string.IsNullOrWhiteSpace(error))
                    _error = error.Length > 240 ? error[..240] : error;
            }
        }

        public bool TryComplete(
            string fallbackState,
            out DestinationTelemetry? destination,
            out string state)
        {
            lock (_gate)
            {
                if (_completedAtUtc is not null)
                {
                    destination = null;
                    state = _state;
                    return false;
                }

                if (_state == "active")
                    _state = fallbackState;
                _completedAtUtc = DateTime.UtcNow;
                destination = _destination;
                state = _state;
                return true;
            }
        }

        public NetworkProxyConnectionSnapshot ToSnapshot()
        {
            lock (_gate)
            {
                return new NetworkProxyConnectionSnapshot
                {
                    Id = Id,
                    Host = _host,
                    Port = _port,
                    Protocol = _protocol,
                    StartedAtUtc = StartedAtUtc,
                    CompletedAtUtc = _completedAtUtc,
                    BytesUploaded = _bytesUploaded,
                    BytesDownloaded = _bytesDownloaded,
                    ConnectLatencyMs = _connectLatencyMs,
                    State = _state,
                    Error = _error
                };
            }
        }
    }

    private sealed class DestinationTelemetry
    {
        private readonly object _gate = new();
        private int _activeConnections;
        private long _totalConnections;
        private long _bytesUploaded;
        private long _bytesDownloaded;
        private long? _lastConnectLatencyMs;
        private string _lastState = "active";
        private DateTime _lastUpdatedAtUtc = DateTime.UtcNow;

        public DestinationTelemetry(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }

        public int Port { get; }

        public void Begin()
        {
            lock (_gate)
            {
                _activeConnections++;
                _totalConnections++;
                _lastState = "active";
                _lastUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        public void SetConnectLatency(long latencyMs)
        {
            lock (_gate)
            {
                _lastConnectLatencyMs = Math.Max(0, latencyMs);
                _lastUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        public void AddUploaded(int bytes)
        {
            lock (_gate)
            {
                _bytesUploaded += bytes;
                _lastUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        public void AddDownloaded(int bytes)
        {
            lock (_gate)
            {
                _bytesDownloaded += bytes;
                _lastUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        public void Complete(string state)
        {
            lock (_gate)
            {
                _activeConnections = Math.Max(0, _activeConnections - 1);
                _lastState = state;
                _lastUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        public NetworkProxyDestinationSnapshot ToSnapshot()
        {
            lock (_gate)
            {
                return new NetworkProxyDestinationSnapshot
                {
                    Host = Host,
                    Port = Port,
                    ActiveConnections = _activeConnections,
                    TotalConnections = _totalConnections,
                    BytesUploaded = _bytesUploaded,
                    BytesDownloaded = _bytesDownloaded,
                    LastConnectLatencyMs = _lastConnectLatencyMs,
                    LastState = _lastState,
                    LastUpdatedAtUtc = _lastUpdatedAtUtc
                };
            }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
