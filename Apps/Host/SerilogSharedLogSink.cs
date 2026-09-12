using System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Shared.Logging;

namespace UniversalDeviceToolkit.Host;

internal sealed class SerilogSharedLogSink : ISharedLogSink
{
    public bool IsTraceEnabled => Log.Instance.IsTraceEnabled;

    public void Trace(string message, Exception? ex = null) => Log.Instance.Trace(message, ex);

    public void Warning(string message, Exception? ex = null) => Log.Instance.Warning(message, ex);

    public void Info(string message, Exception? ex = null) => Log.Instance.Info(message, ex);

    public void Error(string message, Exception? ex = null) => Log.Instance.Error(message, ex);
}
