using System;
using System.Threading;

namespace UniversalDeviceToolkit.Shared.Logging;

/// <summary>
/// Minimal cross-platform logging façade.
/// Replaces the Serilog-backed Lib.Utils.Log for the Shared layer.
/// Consumers that need full structured logging should wire up their own
/// <see cref="ISharedLogSink"/> at application startup.
/// </summary>
public static class SharedLog
{
    private static ISharedLogSink? _sink;

    /// <summary>
    /// Register a sink that receives all log calls from the Shared layer.
    /// Typically set once at application startup by the host (Electron/.NET Host / CLI / tests).
    /// </summary>
    public static void SetSink(ISharedLogSink? sink) => _sink = sink;

    public static bool IsTraceEnabled
    {
        get => _sink?.IsTraceEnabled ?? false;
    }

    public static void Trace(string message, Exception? ex = null)
        => _sink?.Trace(message, ex);

    public static void Warning(string message, Exception? ex = null)
        => _sink?.Warning(message, ex);

    public static void Info(string message, Exception? ex = null)
        => _sink?.Info(message, ex);

    public static void Error(string message, Exception? ex = null)
        => _sink?.Error(message, ex);
}

/// <summary>
/// Sink interface for receiving Shared-layer log events.
/// Implement this to bridge to Serilog, NLog, Console, or test harnesses.
/// </summary>
public interface ISharedLogSink
{
    bool IsTraceEnabled { get; }
    void Trace(string message, Exception? ex = null);
    void Warning(string message, Exception? ex = null);
    void Info(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
}
