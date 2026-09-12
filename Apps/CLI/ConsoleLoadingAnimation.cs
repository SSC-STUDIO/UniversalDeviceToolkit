using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.CLI;

internal sealed class ConsoleLoadingAnimation : IDisposable
{
    private const int IntervalMilliseconds = 250;
    private static readonly string[] Frames = ["|", "/", "-", "\\"];

    private readonly CancellationTokenSource _cts = new();
    private readonly Task? _task;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly string _message;
    private readonly bool _enabled;
    private int _lastLength;
    private bool _disposed;

    private ConsoleLoadingAnimation(string message, bool enabled)
    {
        _message = message;
        _enabled = enabled;

        if (_enabled)
            _task = Task.Run(RenderLoopAsync);
    }

    public static ConsoleLoadingAnimation Start(string message, bool enabled = true)
        => new(message, enabled && IsInteractiveConsole());

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cts.Cancel();
        try
        {
            _task?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine($"[CLI] Loading animation task cancelled: {ex.Message}");
        }

        if (_enabled)
            ClearLine();

        _cts.Dispose();
    }

    private async Task RenderLoopAsync()
    {
        var frameIndex = 0;

        while (!_cts.IsCancellationRequested)
        {
            WriteFrame(Frames[frameIndex % Frames.Length]);
            frameIndex = (frameIndex + 1) % Frames.Length;
            await Task.Delay(IntervalMilliseconds, _cts.Token).ConfigureAwait(false);
        }
    }

    private void WriteFrame(string frame)
    {
        var elapsed = _stopwatch.Elapsed;
        var text = $"{frame} {_message} ({elapsed:mm\\:ss})";
        var width = GetConsoleWidth();

        if (width > 0 && text.Length >= width)
            text = text[..Math.Max(0, width - 1)];

        var padding = Math.Max(0, _lastLength - text.Length);
        Console.Error.Write('\r');
        Console.Error.Write(text);
        if (padding > 0)
            Console.Error.Write(new string(' ', padding));

        _lastLength = text.Length;
    }

    private void ClearLine()
    {
        Console.Error.Write('\r');
        if (_lastLength > 0)
            Console.Error.Write(new string(' ', _lastLength));
        Console.Error.Write('\r');
    }

    private static bool IsInteractiveConsole()
    {
        if (Console.IsErrorRedirected)
            return false;

        return GetConsoleWidth() > 0;
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch
        {
            return 0;
        }
    }
}
