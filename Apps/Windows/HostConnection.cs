using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Owns a Host process and its newline-delimited JSON-RPC session.</summary>
internal sealed class HostConnection(string executable, IReadOnlyList<string> arguments, Action<string> log) : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private TaskCompletionSource _ready = NewReady();
    private Process? _process;
    private Task? _supervisor;
    private string? _lastError;
    private JsonElement? _readyPayload;
    private volatile bool _stopping;
    private long _nextId;

    public event Action<string, JsonElement>? EventReceived;

    private static TaskCompletionSource NewReady() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public object Status
    {
        get
        {
            lock (_gate)
                return new { running = _process is { HasExited: false }, ready = _ready.Task.IsCompletedSuccessfully && _process is { HasExited: false }, lastError = _lastError, readyPayload = _readyPayload };
        }
    }

    public int? ProcessId { get { lock (_gate) return _process is { HasExited: false } process ? process.Id : null; } }
    public JsonElement? ReadyPayload { get { lock (_gate) return _ready.Task.IsCompletedSuccessfully && _process is { HasExited: false } ? _readyPayload : null; } }

    public void Start()
    {
        lock (_gate)
        {
            if (_supervisor != null) throw new InvalidOperationException("Host supervision has already started.");
            _supervisor = Task.Run(SuperviseAsync);
        }
    }

    public async Task<JsonElement> InvokeAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        Task ready;
        lock (_gate) ready = _ready.Task;
        await ready.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Process process;
                lock (_gate) process = _process ?? throw new IOException("The Host is not running.");
                var line = JsonSerializer.Serialize(new { id, method, @params = parameters });
                await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { _writer.Release(); }
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task SuperviseAsync()
    {
        for (var attempt = 0; attempt <= 3 && !_lifetime.IsCancellationRequested; attempt++)
        {
            if (attempt > 0)
            {
                try { await Task.Delay(750, _lifetime.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            lock (_gate)
            {
                if (attempt > 0) _ready = NewReady();
                _readyPayload = null;
            }
            using var process = new Process { StartInfo = CreateStartInfo() };
            Task? output = null;
            Task? errors = null;
            try
            {
                if (!process.Start()) throw new IOException("Unable to start the Host process.");
                lock (_gate) _process = process;
                output = ReadOutputAsync(process);
                errors = ReadErrorsAsync(process);
                var exit = process.WaitForExitAsync(_lifetime.Token);
                var ready = _ready.Task;
                var first = await Task.WhenAny(ready, exit, output, Task.Delay(TimeSpan.FromSeconds(15), _lifetime.Token)).ConfigureAwait(false);
                if (first != ready) throw new IOException("Host exited or timed out before becoming ready.");
                await ready.ConfigureAwait(false);
                var terminal = await Task.WhenAny(exit, output).ConfigureAwait(false);
                await output.ConfigureAwait(false);
                if (terminal == output && !process.HasExited && !_stopping) throw new IOException("The Host closed its RPC output stream.");
                await exit.ConfigureAwait(false);
                if (!_stopping) throw new IOException($"Host exited unexpectedly (code {process.ExitCode}).");
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (!_stopping)
                {
                    lock (_gate) _lastError = error.Message;
                    log(error.ToString());
                    Emit("host.error", JsonSerializer.SerializeToElement(new { message = error.Message, fatal = attempt == 3 }));
                }
            }
            finally
            {
                lock (_gate) _process = null;
                var failure = new IOException(_lastError ?? "Host stopped.");
                _ready.TrySetException(failure);
                foreach (var (id, completion) in _pending)
                    if (_pending.TryRemove(id, out _)) completion.TrySetException(failure);
                try { if (process.Id > 0 && !process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException error) { log(error.Message); }
                catch (System.ComponentModel.Win32Exception error) { log(error.Message); }
                foreach (var reader in new[] { output, errors })
                {
                    if (reader == null) continue;
                    try { await reader.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                    catch (Exception error) when (error is not OutOfMemoryException) { log(error.Message); }
                }
            }
            if (_stopping) break;
        }
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["UDT_SHELL_PATH"] = Environment.ProcessPath;
        info.Environment["UDT_SHELL_ARGS"] = "--minimized";
        return info;
    }

    private async Task ReadOutputAsync(Process process)
    {
        while (await process.StandardOutput.ReadLineAsync(_lifetime.Token).ConfigureAwait(false) is { } line)
        {
            lock (_gate) { if (!ReferenceEquals(process, _process)) return; }
            JsonDocument document;
            try { document = JsonDocument.Parse(line); }
            catch (JsonException) { log(line); continue; }
            using (document)
            {
                var message = document.RootElement;
                if (message.ValueKind != JsonValueKind.Object) continue;
                if (message.TryGetProperty("event", out var eventName) && eventName.ValueKind == JsonValueKind.String && eventName.GetString() is { } name)
                {
                    var data = message.TryGetProperty("data", out var value) ? value.Clone() : JsonSerializer.SerializeToElement<object?>(null);
                    if (name == "host.ready")
                    {
                        lock (_gate) { _readyPayload = data; _lastError = null; }
                        _ready.TrySetResult();
                    }
                    Emit(name, data);
                }
                else if (message.TryGetProperty("id", out var id) && id.TryGetInt64(out var requestId) && _pending.TryRemove(requestId, out var completion))
                {
                    if (message.TryGetProperty("error", out var error))
                    {
                        var code = error.TryGetProperty("code", out var number) ? number.GetInt32() : -32603;
                        var text = error.TryGetProperty("message", out var description) ? description.GetString() : "Host error";
                        completion.TrySetException(new IOException($"[UDT:{code}] {text}"));
                    }
                    else completion.TrySetResult(message.TryGetProperty("result", out var result) ? result.Clone() : JsonSerializer.SerializeToElement<object?>(null));
                }
            }
        }
    }

    private async Task ReadErrorsAsync(Process process)
    {
        while (await process.StandardError.ReadLineAsync(_lifetime.Token).ConfigureAwait(false) is { } line) log(line);
    }

    private void Emit(string name, JsonElement data)
    {
        try { EventReceived?.Invoke(name, data); }
        catch (Exception error) { log($"Host event listener failed: {error}"); }
    }

    public async ValueTask DisposeAsync()
    {
        _stopping = true;
        if (_ready.Task.IsCompletedSuccessfully)
        {
            using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await InvokeAsync("app.quit", cancellationToken: grace.Token).ConfigureAwait(false); }
            catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException or InvalidOperationException) { log(error.Message); }
        }
        _lifetime.Cancel();
        if (_supervisor != null) await _supervisor.ConfigureAwait(false);
        _lifetime.Dispose();
        _writer.Dispose();
    }
}
