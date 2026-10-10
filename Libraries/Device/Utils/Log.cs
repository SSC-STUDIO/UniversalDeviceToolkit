using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace UniversalDeviceToolkit.Lib.Utils;

public enum LogLevel
{
    Error,
    Warning,
    Info,
    Debug,
    Trace
}

public class Log : IDisposable
{
    private static readonly object _instanceLock = new();
    private static Lazy<Log> _instance = new(() => new Log(), LazyThreadSafetyMode.ExecutionAndPublication);
    public static Log Instance
    {
        get
        {
            lock (_instanceLock)
                return _instance.Value;
        }
    }

    /// <summary>
    /// Replaces the singleton for tests that temporarily redirect AppData.
    /// The production application never changes its logger after startup.
    /// </summary>
    internal static void ResetForTests()
    {
        Lazy<Log> previous;
        lock (_instanceLock)
        {
            previous = _instance;
            _instance = new Lazy<Log>(() => new Log(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        if (previous.IsValueCreated)
            previous.Value.Dispose();
    }

    /// <summary>Keys already emitted by WarningOnce/TraceOnce for this process.</summary>
    private static readonly ConcurrentDictionary<string, byte> _onceKeys = new(StringComparer.Ordinal);

    private readonly Logger _logger;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly string _folderPath;
    private readonly SemaphoreSlim _emergencyLock = new(1, 1);
    private int _disposed;

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public bool IsTraceEnabled
    {
        get => !IsDisposed && _levelSwitch.MinimumLevel <= LogEventLevel.Verbose;
        set
        {
            if (value && !IsDisposed)
                _levelSwitch.MinimumLevel = LogEventLevel.Verbose;
        }
    }

    public LogLevel CurrentLogLevel
    {
        get => IsDisposed ? LogLevel.Error : MapLevelFromSerilog(_levelSwitch.MinimumLevel);
        set
        {
            if (!IsDisposed)
                _levelSwitch.MinimumLevel = MapLevelToSerilog(value);
        }
    }

    public string LogPath => _folderPath;

    /// <summary>
    /// Optional sink used by the Host to forward each Serilog line as a
    /// <c>host.log</c> bridge event for the Electron shell.
    /// </summary>
    public static Action<string>? BridgeLineSink { get; set; }

    private static string ResolveLogFolder()
    {
        var fromEnv = Environment.GetEnvironmentVariable("UDT_LOG_PATH")
            ?? Environment.GetEnvironmentVariable("LLT_LOG_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            Directory.CreateDirectory(fromEnv);
            return fromEnv;
        }

        var folderPath = Path.Combine(Folders.AppData, "logs");
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static Logger CreateSerilogLogger(string folderPath, LoggingLevelSwitch levelSwitch)
    {
        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.WithProperty("Application", AppIdentity.CompactName)
            .WriteTo.Sink(new BridgeLogEventSink())
            .WriteTo.Async(wt => wt.File(
                new Serilog.Formatting.Json.JsonFormatter(),
                Path.Combine(folderPath, "log-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                fileSizeLimitBytes: 50 * 1024 * 1024
            ))
            .WriteTo.Async(wt => wt.File(
                Path.Combine(folderPath, "host.log"),
                outputTemplate: "{Timestamp:o} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 2
            ))
            .CreateLogger();
    }

    private sealed class BridgeLogEventSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var sink = BridgeLineSink;
            if (sink is null)
                return;

            sink($"{logEvent.Timestamp:o} [{logEvent.Level}] {logEvent.RenderMessage()}");
        }
    }

    internal Log(bool _forTesting)
    {
        _folderPath = ResolveLogFolder();
        _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
        _logger = CreateSerilogLogger(_folderPath, _levelSwitch);
    }

    private Log()
    {
        _folderPath = ResolveLogFolder();
        _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
        _logger = CreateSerilogLogger(_folderPath, _levelSwitch);
    }

    public void ErrorReport(string header, Exception ex)
    {
        if (IsDisposed)
            return;

        _logger.Error(ex, "{Header}", header);

        _ = Task.Run(() => WriteErrorReportAsync(header, ex));
    }

    public async Task ErrorReportAsync(string header, Exception ex)
    {
        if (IsDisposed)
            return;

        _logger.Error(ex, "{Header}", header);
        await WriteErrorReportAsync(header, ex).ConfigureAwait(false);
    }

    private async Task WriteErrorReportAsync(string header, Exception ex)
    {
        await _emergencyLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return;

            var suffix = Guid.NewGuid().ToString("N").AsSpan(0, 8).ToString();
            var errorReportPath = Path.Combine(_folderPath, $"error_{DateTime.UtcNow:yyyy_MM_dd_HH_mm_ss_fff}_{suffix}.txt");
            await File.AppendAllLinesAsync(errorReportPath, [header, Serialize(ex)]).ConfigureAwait(false);
        }
        catch
        {
            // Emergency error reports must never propagate to the caller; a failed
            // crash-dump write should not throw during unhandled-exception handling.
        }
        finally
        {
            _emergencyLock.Release();
        }
    }

    public void Error(FormattableString message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        var properties = BuildProperties(sourceContext);
        _logger.Write(LogEventLevel.Error, ex, message.ToString(), properties);
    }

    public void Error(string message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        _logger.Write(LogEventLevel.Error, ex, "{Message} [@{SourceContext}]", message, sourceContext);
    }

    public void Warning(FormattableString message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (CurrentLogLevel < LogLevel.Warning)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        var properties = BuildProperties(sourceContext);
        _logger.Write(LogEventLevel.Warning, ex, message.ToString(), properties);
    }

    public void Warning(string message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (CurrentLogLevel < LogLevel.Warning)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        _logger.Write(LogEventLevel.Warning, ex, "{Message} [@{SourceContext}]", message, sourceContext);
    }

    /// <summary>
    /// Emit a Warning at most once per process for the given key.
    /// Prefer for expected soft-failures that still deserve visibility without flooding logs.
    /// </summary>
    public void WarningOnce(string key, string message, Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (string.IsNullOrEmpty(key) || !_onceKeys.TryAdd(key, 0))
            return;
        Warning(message, ex, file, lineNumber, caller);
    }

    /// <summary>
    /// Emit a Trace at most once per process for the given key (hot paths / capability probes).
    /// </summary>
    public void TraceOnce(string key, string message, Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (string.IsNullOrEmpty(key) || !_onceKeys.TryAdd(key, 0))
            return;
        Trace(message, ex, file, lineNumber, caller);
    }

    public void Info(FormattableString message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (CurrentLogLevel < LogLevel.Info)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        var properties = BuildProperties(sourceContext);
        _logger.Write(LogEventLevel.Information, ex, message.ToString(), properties);
    }

    public void Info(string message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (CurrentLogLevel < LogLevel.Info)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        _logger.Write(LogEventLevel.Information, ex, "{Message} [@{SourceContext}]", message, sourceContext);
    }

    public void Debug(FormattableString message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (CurrentLogLevel < LogLevel.Debug)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        var properties = BuildProperties(sourceContext);
        _logger.Write(LogEventLevel.Debug, ex, message.ToString(), properties);
    }

    public void Debug(string message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (CurrentLogLevel < LogLevel.Debug)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        _logger.Write(LogEventLevel.Debug, ex, "{Message} [@{SourceContext}]", message, sourceContext);
    }

    public void Trace(FormattableString message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (!IsTraceEnabled && CurrentLogLevel < LogLevel.Trace)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        var properties = BuildProperties(sourceContext);
        _logger.Write(LogEventLevel.Verbose, ex, message.ToString(), properties);
    }

    public void Trace(string message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (IsDisposed)
            return;

        if (!IsTraceEnabled && CurrentLogLevel < LogLevel.Trace)
            return;

        var sourceContext = FormatSourceContext(file, lineNumber, caller);
        _logger.Write(LogEventLevel.Verbose, ex, "{Message} [@{SourceContext}]", message, sourceContext);
    }

    public void Flush()
    {
        // Serilog's async sink flushes on its own timer; there is no
        // non-destructive synchronous flush on Logger.  Calling Shutdown()
        // here would permanently disable the singleton, so this is intentionally
        // a no-op: the AsyncSink's internal block flushes automatically within
        // its configured period (~5 s).
    }

    public async Task ShutdownAsync()
    {
        // DisposeCore is async-safe and idempotent: only the first caller wins the
        // CAS, guaranteeing _logger and _emergencyLock are each disposed exactly once
        // regardless of whether Shutdown, ShutdownAsync, or Dispose fires first.
        await DisposeCoreAsync().ConfigureAwait(false);
    }

    public void Shutdown()
    {
        // Non-blocking, UI-Dispatcher-safe teardown with a 2s timeout guard.
        var t = ShutdownAsync();
        if (t.IsCompletedSuccessfully)
            return;
        if (!t.Wait(TimeSpan.FromSeconds(2)))
        {
            try { t.ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously); } catch { }
        }
    }

    public void Dispose()
    {
        // Synchronous IDisposable contract, same timeout-guarded non-blocking strategy.
        var t = DisposeCoreAsync();
        if (t.IsCompletedSuccessfully)
            return;
        if (!t.Wait(TimeSpan.FromSeconds(2)))
        {
            try { t.ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously); } catch { }
        }
    }


    private static string FormatSourceContext(string? file, int lineNumber, string? caller)
    {
        var fileName = file is not null ? Path.GetFileName(file) : "?";
        return $"{fileName}#{lineNumber}:{caller}";
    }

    private static object[] BuildProperties(string sourceContext)
    {
        return [sourceContext, Environment.CurrentManagedThreadId];
    }

    private static LogEventLevel MapLevelToSerilog(LogLevel level) => level switch
    {
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Info => LogEventLevel.Information,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Trace => LogEventLevel.Verbose,
        _ => LogEventLevel.Verbose
    };

    private static LogLevel MapLevelFromSerilog(LogEventLevel level) => level switch
    {
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Error,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Information => LogLevel.Info,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Verbose => LogLevel.Trace,
        _ => LogLevel.Trace
    };

    private static string Serialize(Exception ex) => new StringBuilder()
        .AppendLine("=== Exception ===")
        .AppendLine(ex.ToString())
        .ToString();


    private async Task DisposeCoreAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        var logger = _logger;
        var emergencyLock = _emergencyLock;

        // Dispose the Serilog logger off the caller thread to avoid blocking the
        // UI Dispatcher / unhandled-exception handler on the async sink flush.
        await Task.Run(() => logger.Dispose()).ConfigureAwait(false);

        // The emergency-lock semaphore is a managed wrapper around a native wait
        // handle; it must be released exactly once, even when teardown is reached
        // through Shutdown/ShutdownAsync rather than Dispose.
        emergencyLock.Dispose();
    }
}
