using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Named-pipe client for the NetworkProxy worker (JSON line protocol).</summary>
public sealed class NetworkProxyIpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _pipeName;
    private readonly string _sessionToken;

    public NetworkProxyIpcClient(string pipeName, string sessionToken)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? NetworkAccelerationDefaults.DefaultPipeName
            : pipeName.Trim();
        _sessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));

        if (!NetworkProxySessionToken.IsValidFormat(_sessionToken))
            throw new ArgumentException("Session token must be a non-empty random token.", nameof(sessionToken));
    }

    public Task<NetworkProxyIpcResult> StatusAsync(CancellationToken cancellationToken = default) =>
        SendAsync("status", payload: null, cancellationToken);

    public Task<NetworkProxyIpcResult> ConnectionsAsync(CancellationToken cancellationToken = default) =>
        SendAsync("connections", payload: null, cancellationToken);

    public Task<NetworkProxyIpcResult> DestinationsAsync(CancellationToken cancellationToken = default) =>
        SendAsync("destinations", payload: null, cancellationToken);

    public Task<NetworkProxyIpcResult> StartAsync(CancellationToken cancellationToken = default) =>
        SendAsync("start", payload: null, cancellationToken);

    public Task<NetworkProxyIpcResult> StopAsync(CancellationToken cancellationToken = default) =>
        SendAsync("stop", payload: null, cancellationToken);

    public Task<NetworkProxyIpcResult> ShutdownAsync(CancellationToken cancellationToken = default) =>
        SendAsync("shutdown", payload: null, cancellationToken);

    /// <summary>
    /// Pushes domain allowlist to the worker (JSON string array payload on the <c>rules</c> op).
    /// Empty list means allow-all on the host (full-proxy path).
    /// </summary>
    public Task<NetworkProxyIpcResult> SetRulesAsync(
        IReadOnlyList<string>? domains,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(domains ?? Array.Empty<string>(), JsonOptions);
        return SendAsync("rules", payload, cancellationToken);
    }

    public async Task<NetworkProxyIpcResult> SendAsync(
        string operation,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(5));
        await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

        using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, Encoding.UTF8, bufferSize: 4096, leaveOpen: true) { AutoFlush = true };

        var request = new NetworkProxyIpcWireRequest
        {
            Operation = operation,
            Token = _sessionToken,
            Payload = payload
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);

        // Read with a timeout so a hung server does not block the caller forever.
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readCts.CancelAfter(TimeSpan.FromSeconds(10));

        string? line;
        try
        {
            line = await reader.ReadLineAsync(readCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NetworkProxyIpcResult.Fail("read timed out after 10 s");
        }
        if (string.IsNullOrWhiteSpace(line))
            return NetworkProxyIpcResult.Fail("empty response");

        NetworkProxyIpcWireResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<NetworkProxyIpcWireResponse>(line, JsonOptions);
        }
        catch (JsonException ex)
        {
            return NetworkProxyIpcResult.Fail($"invalid json: {ex.Message}");
        }

        if (response is null)
            return NetworkProxyIpcResult.Fail("null response");

        return new NetworkProxyIpcResult(response.Success, response.Message, response.Data);
    }

    private sealed class NetworkProxyIpcWireRequest
    {
        public string? Operation { get; set; }
        public string? Token { get; set; }
        public string? Payload { get; set; }
    }

    private sealed class NetworkProxyIpcWireResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, string>? Data { get; set; }
    }
}

public readonly record struct NetworkProxyIpcResult(
    bool Success,
    string? Message,
    Dictionary<string, string>? Data)
{
    public static NetworkProxyIpcResult Fail(string message) => new(false, message, null);
}
