using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Newline-delimited JSON-RPC-ish protocol over the host process's stdio.
///
/// Requests (client -> host, one JSON object per line):
///   {"id":1,"method":"ping","params":{}}
/// Responses (host -> client):
///   {"id":1,"result":{...}}  |  {"id":1,"error":{"code":-32601,"message":"..."}}
/// Events (host -> client, no id):
///   {"event":"host.ready","data":{...}}
/// </summary>
public enum BridgeParseStatus
{
    Empty,
    Event,
    Ok,
    InvalidJson,
    InvalidRequest,
}

/// <summary>
/// Encode/decode helpers for the Host stdio bridge protocol.
/// </summary>
public static class BridgeProtocol
{
    public const string EventProperty = "event";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Parses a single line into either a request or an event envelope.
    /// Returns null when the line is not a valid protocol message.
    /// </summary>
    public static bool TryParseRequest(string line, out BridgeRequest? request)
        => TryParseLine(line, out request, out _) == BridgeParseStatus.Ok;

    /// <summary>
    /// Classifies one stdio line: valid request, event, empty, parse error, or
    /// invalid request (object without a method / malformed id).
    /// </summary>
    public static BridgeParseStatus TryParseLine(string line, out BridgeRequest? request, out long? id)
    {
        request = null;
        id = null;
        if (string.IsNullOrWhiteSpace(line))
            return BridgeParseStatus.Empty;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return BridgeParseStatus.InvalidJson;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return BridgeParseStatus.InvalidRequest;

            if (!TryReadId(root, out id, out var idMalformed) || idMalformed)
                return BridgeParseStatus.InvalidRequest;

            if (root.TryGetProperty(EventProperty, out _))
                return BridgeParseStatus.Event;

            if (!root.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String)
                return BridgeParseStatus.InvalidRequest;

            var methodName = method.GetString();
            if (string.IsNullOrWhiteSpace(methodName))
                return BridgeParseStatus.InvalidRequest;

            var parameters = root.TryGetProperty("params", out var paramsProp)
                ? paramsProp.Clone()
                : default;

            request = new BridgeRequest(id, methodName, parameters);
            return BridgeParseStatus.Ok;
        }
    }

    private static bool TryReadId(JsonElement root, out long? id, out bool malformed)
    {
        id = null;
        malformed = false;
        if (!root.TryGetProperty("id", out var idProp) || idProp.ValueKind == JsonValueKind.Null)
            return true;

        if (idProp.ValueKind == JsonValueKind.Number)
        {
            if (idProp.TryGetInt64(out var number))
            {
                id = number;
                return true;
            }

            malformed = true;
            return false;
        }

        if (idProp.ValueKind == JsonValueKind.String)
        {
            var text = idProp.GetString();
            if (long.TryParse(text, out var parsed))
            {
                id = parsed;
                return true;
            }

            malformed = true;
            return false;
        }

        malformed = true;
        return false;
    }

    /// <summary>
    /// Writes {"id":N,"result":...} in one pass: the handler result is serialized
    /// directly into the response frame (no intermediate JsonDocument round-trip).
    /// </summary>
    public static void WriteResultResponse(Utf8JsonWriter writer, long? id, object? result)
    {
        writer.WriteStartObject();
        WriteId(writer, id);
        writer.WritePropertyName("result");
        if (result is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, result, result.GetType(), JsonOptions);
        writer.WriteEndObject();
    }

    /// <summary>Writes {"id":N,"error":{"code":...,"message":...}}.</summary>
    public static void WriteErrorResponse(Utf8JsonWriter writer, long? id, int errorCode, string? errorMessage)
    {
        writer.WriteStartObject();
        WriteId(writer, id);
        writer.WritePropertyName("error");
        writer.WriteStartObject();
        writer.WriteNumber("code", errorCode);
        writer.WriteString("message", errorMessage ?? "Unknown error");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>Writes {"event":name,"data":...}.</summary>
    public static void WriteEvent(Utf8JsonWriter writer, string name, object? data)
    {
        writer.WriteStartObject();
        writer.WriteString(EventProperty, name);
        writer.WritePropertyName("data");
        if (data is null)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, data, data.GetType(), JsonOptions);
        writer.WriteEndObject();
    }

    private static void WriteId(Utf8JsonWriter writer, long? id)
    {
        if (id is not null)
            writer.WriteNumber("id", id.Value);
        else
            writer.WriteNull("id");
    }
}

public sealed class BridgeRequest
{
    public BridgeRequest(long? id, string method, JsonElement parameters)
    {
        Id = id;
        Method = method;
        Parameters = parameters;
    }

    public long? Id { get; }
    public string Method { get; }
    public JsonElement Parameters { get; }
}

/// <summary>
/// Result of dispatching a bridge request. Carries either a serializable result
/// or an error tuple.
/// </summary>
public sealed class BridgeResult
{
    private BridgeResult(object? value, int? errorCode, string? errorMessage)
    {
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public object? Value { get; }
    public int? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public bool IsError => ErrorCode is not null;

    public static BridgeResult Ok(object? value = null) => new(value, null, null);
    public static BridgeResult Error(int code, string message) => new(null, code, message);
}

/// <summary>
/// Reads requests from stdin, dispatches them to registered handlers and writes
/// responses/events to stdout. Single writer lock keeps each JSON line atomic.
/// Frame size, pending count and handler concurrency are bounded so a noisy
/// renderer or plugin cannot exhaust memory or thread-pool work.
/// </summary>
public sealed class BridgeRpcServer : IDisposable
{
    public const int MaxFrameBytes = 1_048_576;
    public const int MaxPendingRequests = 64;
    public const int MaxConcurrentHandlers = 16;
    private const int DrainTimeoutMs = 2000;

    private readonly Dictionary<string, Func<BridgeRequest, CancellationToken, Task<BridgeResult>>> _handlers = new(StringComparer.Ordinal);
    private readonly object _writeLock = new();
    private readonly object _inflightLock = new();
    private readonly HashSet<Task> _inflight = new();
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly BoundedLineReader _lineReader;
    private const int FrameBufferInitialBytes = 4096;
    /// <summary>Rare oversized frames must not pin their buffer for the process lifetime.</summary>
    private const int FrameBufferRetentionBytes = 256 * 1024;

    // Frame buffer + writer are reused for every outgoing message; both are only
    // touched under _writeLock, so a single instance is safe and steady-state
    // writes allocate nothing for the framing itself.
    private ArrayBufferWriter<byte> _frameBuffer = new(FrameBufferInitialBytes);
    private readonly Utf8JsonWriter _frameWriter;
    private readonly SemaphoreSlim _concurrency = new(MaxConcurrentHandlers, MaxConcurrentHandlers);
    private readonly CancellationTokenSource _cts = new();
    private int _pending;
    private int _disposed;

    /// <summary>Raised when the client closes the pipe (Electron exited).</summary>
    public event Action? ClientDisconnected;

    public BridgeRpcServer()
    {
        _input = Console.OpenStandardInput();
        _output = Console.OpenStandardOutput();
        _lineReader = new BoundedLineReader(_input);
        _frameWriter = new Utf8JsonWriter(_frameBuffer);
    }

    public void RegisterHandler(string method, Func<BridgeRequest, CancellationToken, Task<BridgeResult>> handler)
        => _handlers[method] = handler;

    public void RegisterHandler(string method, Func<BridgeRequest, Task<BridgeResult>> handler)
        => RegisterHandler(method, (request, _) => handler(request));

    /// <summary>True when a handler is registered for the method.</summary>
    public bool HasHandler(string method) => _handlers.ContainsKey(method);

    /// <summary>Registered method names (startup surface verification).</summary>
    public IReadOnlyCollection<string> RegisteredMethods => _handlers.Keys;

    /// <summary>
    /// Blocks reading stdin until EOF or <see cref="RequestShutdown"/>.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        try
        {
            while (!linked.IsCancellationRequested)
            {
                BoundedLineStatus status;
                string? line;
                try
                {
                    (status, line) = await _lineReader.ReadLineAsync(MaxFrameBytes, linked.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (status == BoundedLineStatus.Eof)
                    break;

                if (status == BoundedLineStatus.TooLarge)
                {
                    WriteError(null, BridgeErrorCodes.RequestTooLarge,
                        $"Request frame exceeds {MaxFrameBytes} bytes.");
                    continue;
                }

                if (line is null || string.IsNullOrWhiteSpace(line))
                    continue;

                var parse = BridgeProtocol.TryParseLine(line, out var request, out var id);
                if (parse == BridgeParseStatus.Empty || parse == BridgeParseStatus.Event)
                    continue;

                if (parse == BridgeParseStatus.InvalidJson)
                {
                    WriteError(id, BridgeErrorCodes.ParseError, "Invalid JSON.");
                    continue;
                }

                if (parse != BridgeParseStatus.Ok || request is null)
                {
                    WriteError(id, BridgeErrorCodes.InvalidRequest, "Invalid request.");
                    continue;
                }

                var pending = Interlocked.Increment(ref _pending);
                if (pending > MaxPendingRequests)
                {
                    Interlocked.Decrement(ref _pending);
                    WriteError(request.Id, BridgeErrorCodes.TooManyRequests,
                        $"Too many pending requests (limit {MaxPendingRequests}).");
                    continue;
                }

                TrackInflight(DispatchAsync(request, linked.Token));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutdown is requested.
        }

        await DrainInflightAsync().ConfigureAwait(false);
        ClientDisconnected?.Invoke();
    }

    private void TrackInflight(Task task)
    {
        lock (_inflightLock)
            _inflight.Add(task);

        _ = AwaitInflightAsync(task);
    }

    private async Task AwaitInflightAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // DispatchAsync already converts handler failures into RPC errors.
        }
        finally
        {
            lock (_inflightLock)
                _inflight.Remove(task);
        }
    }

    private async Task DrainInflightAsync()
    {
        Task[] snapshot;
        lock (_inflightLock)
            snapshot = _inflight.Count == 0 ? Array.Empty<Task>() : _inflight.ToArray();

        if (snapshot.Length == 0)
            return;

        try
        {
            var drain = Task.WhenAll(snapshot);
            await Task.WhenAny(drain, Task.Delay(DrainTimeoutMs)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort drain.
        }
    }

    private async Task DispatchAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            BridgeResult result;
            try
            {
                await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                WriteError(request.Id, BridgeErrorCodes.RequestCancelled, "Request cancelled");
                return;
            }

            try
            {
                if (!_handlers.TryGetValue(request.Method, out var handler))
                {
                    result = BridgeResult.Error(BridgeErrorCodes.UnknownMethod, $"Unknown method: {request.Method}");
                }
                else
                {
                    result = await handler(request, cancellationToken).ConfigureAwait(false) ?? BridgeResult.Ok(null);
                }
            }
            catch (OperationCanceledException)
            {
                result = BridgeResult.Error(BridgeErrorCodes.RequestCancelled, "Request cancelled");
            }
            catch (Exception ex)
            {
                result = BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _concurrency.Release();
            }

            if (request.Id is not null)
            {
                if (result.IsError)
                    WriteError(request.Id, result.ErrorCode ?? BridgeErrorCodes.InternalError, result.ErrorMessage ?? "Unknown error");
                else
                    WriteResult(request.Id, result.Value);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
        }
    }

    public void Publish(string name, object? data)
    {
        lock (_writeLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            try
            {
                BeginFrame();
                BridgeProtocol.WriteEvent(_frameWriter, name, data);
                EndFrame();
            }
            catch (Exception)
            {
                // Unserializable event payload; the partial buffer is discarded
                // by the reset at the start of the next frame.
                return;
            }
            WriteFrameLocked();
        }
    }

    private void WriteResult(long? id, object? value)
    {
        lock (_writeLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            try
            {
                BeginFrame();
                BridgeProtocol.WriteResultResponse(_frameWriter, id, value);
                EndFrame();
            }
            catch (Exception)
            {
                // A handler result that cannot be serialized must still answer
                // the request, otherwise the client would spin until its timeout.
                BeginFrame();
                BridgeProtocol.WriteErrorResponse(_frameWriter, id, BridgeErrorCodes.InternalError, "Result serialization failed.");
                EndFrame();
            }
            WriteFrameLocked();
        }
    }

    private void WriteError(long? id, int code, string message)
    {
        lock (_writeLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            BeginFrame();
            BridgeProtocol.WriteErrorResponse(_frameWriter, id, code, message);
            EndFrame();
            WriteFrameLocked();
        }
    }

    /// <summary>Resets the shared frame buffer/writer for one message. Caller holds _writeLock.</summary>
    private void BeginFrame()
    {
        _frameBuffer.ResetWrittenCount();
        _frameWriter.Reset(_frameBuffer);
    }

    /// <summary>Flushes the JSON writer and appends the protocol newline. Caller holds _writeLock.</summary>
    private void EndFrame()
    {
        _frameWriter.Flush();
        _frameBuffer.GetSpan(1)[0] = (byte)'\n';
        _frameBuffer.Advance(1);
    }

    /// <summary>Writes the buffered frame to stdout in a single call. Caller holds _writeLock.</summary>
    private void WriteFrameLocked()
    {
        try
        {
            _output.Write(_frameBuffer.WrittenSpan);
            _output.Flush();
        }
        catch (IOException)
        {
            // Pipe closed by the client; ignore.
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }
        finally
        {
            if (_frameBuffer.Capacity > FrameBufferRetentionBytes)
                _frameBuffer = new ArrayBufferWriter<byte>(FrameBufferInitialBytes);
        }
    }

    public void RequestShutdown() => _cts.Cancel();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _concurrency.Dispose();
        try
        {
            lock (_writeLock)
                _frameWriter.Dispose();
            _output.Dispose();
            _input.Dispose();
        }
        catch (Exception)
        {
            // Best-effort disposal.
        }
    }

    private enum BoundedLineStatus
    {
        Ok,
        Eof,
        TooLarge,
    }

    private sealed class BoundedLineReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[8192];
        private byte[] _line = new byte[4096];
        private int _lineLength;
        private int _buffered;
        private int _offset;

        public BoundedLineReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<(BoundedLineStatus Status, string? Line)> ReadLineAsync(
            int maxBytes,
            CancellationToken cancellationToken)
        {
            _lineLength = 0;
            var overflow = false;

            while (true)
            {
                if (_offset >= _buffered)
                {
                    _buffered = await _stream.ReadAsync(_buffer.AsMemory(0, _buffer.Length), cancellationToken)
                        .ConfigureAwait(false);
                    _offset = 0;
                    if (_buffered == 0)
                    {
                        if (_lineLength == 0 && !overflow)
                            return (BoundedLineStatus.Eof, null);
                        return overflow
                            ? (BoundedLineStatus.TooLarge, null)
                            : (BoundedLineStatus.Ok, Encoding.UTF8.GetString(_line, 0, _lineLength));
                    }
                }

                // Scan the buffered chunk for the newline and copy in one block
                // instead of accumulating byte-by-byte.
                var newlineIndex = Array.IndexOf(_buffer, (byte)'\n', _offset, _buffered - _offset);
                var chunkEnd = newlineIndex >= 0 ? newlineIndex : _buffered;
                var chunkLength = chunkEnd - _offset;

                if (!overflow && chunkLength > 0)
                {
                    if (_lineLength + chunkLength > maxBytes)
                    {
                        overflow = true;
                        _lineLength = 0;
                    }
                    else
                    {
                        EnsureLineCapacity(_lineLength + chunkLength, maxBytes);
                        Buffer.BlockCopy(_buffer, _offset, _line, _lineLength, chunkLength);
                        _lineLength += chunkLength;
                    }
                }

                if (newlineIndex < 0)
                {
                    _offset = _buffered;
                    continue;
                }

                _offset = newlineIndex + 1;
                if (overflow)
                    return (BoundedLineStatus.TooLarge, null);
                if (_lineLength > 0 && _line[_lineLength - 1] == (byte)'\r')
                    _lineLength--;
                return (BoundedLineStatus.Ok, Encoding.UTF8.GetString(_line, 0, _lineLength));
            }
        }

        private void EnsureLineCapacity(int required, int maxBytes)
        {
            if (_line.Length >= required)
                return;
            var newSize = Math.Max(_line.Length * 2, required);
            if (newSize > maxBytes)
                newSize = maxBytes;
            var grown = new byte[newSize];
            Buffer.BlockCopy(_line, 0, grown, 0, _lineLength);
            _line = grown;
        }
    }
}
