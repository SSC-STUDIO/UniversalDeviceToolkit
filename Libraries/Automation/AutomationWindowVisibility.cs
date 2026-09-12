using System;
using System.Threading;
using UniversalDeviceToolkit.Lib.Messaging.Messages;

namespace UniversalDeviceToolkit.Lib.Automation;

/// <summary>
/// Host registers a single handler that forwards main-window show/hide requests
/// to the Electron client as <see cref="HostEventName"/>. Automation steps fail
/// if no handler is registered instead of publishing an unsubscribed message.
/// </summary>
public static class AutomationWindowVisibility
{
    public const string HostEventName = "window.visibility";

    private static readonly object Gate = new();
    private static Action<MainWindowVisibilityAction>? _handler;

    public static bool IsBridged
    {
        get
        {
            lock (Gate)
                return _handler is not null;
        }
    }

    public static IDisposable Register(Action<MainWindowVisibilityAction> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (Gate)
            _handler = handler;

        return new Registration(handler);
    }

    public static void Request(MainWindowVisibilityAction action)
    {
        Action<MainWindowVisibilityAction>? handler;
        lock (Gate)
            handler = _handler;

        if (handler is null)
            throw new InvalidOperationException("Main window visibility is not bridged to the UI host.");

        handler(action);
    }

    private sealed class Registration(Action<MainWindowVisibilityAction> handler) : IDisposable
    {
        private Action<MainWindowVisibilityAction>? _handler = handler;

        public void Dispose()
        {
            var handler = Interlocked.Exchange(ref _handler, null);
            if (handler is null)
                return;

            lock (Gate)
            {
                if (ReferenceEquals(AutomationWindowVisibility._handler, handler))
                    AutomationWindowVisibility._handler = null;
            }
        }
    }
}
