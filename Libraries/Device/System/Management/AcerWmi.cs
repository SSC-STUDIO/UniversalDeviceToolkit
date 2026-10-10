using System;
using System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Management;

/// <summary>
/// Acer "WMID Gaming" WMI channel (protocol mirrors the mainline acer-wmi
/// driver, GUID 7A4DDFE7-…): root\wmi class AcerGamingFunction, methods take a
/// uint32 gmInput and return gmOutput whose low byte is the status (0 = ok).
/// Never throws; absence of the interface is reported via <see cref="IsAvailable"/>.
/// </summary>
public interface IAcerWmi
{
    bool IsAvailable { get; }

    /// <summary>Invokes a gaming method; returns (ok, raw gmOutput).</summary>
    (bool Ok, long Output) Execute(string methodName, uint input);
}

public sealed class AcerWmi : IAcerWmi, IDisposable
{
    private const string Namespace = @"root\wmi";
    private const string MethodClass = "AcerGamingFunction";
    private const string PreferredInstance = @"ACPI\PNP0C14\APGe_0";

    private readonly object _lock = new();
    private bool _initialized;
    private bool _disposed;
    private ManagementObject? _methodObject;

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _methodObject is not null;
        }
    }

    public (bool Ok, long Output) Execute(string methodName, uint input)
    {
        if (!IsAvailable)
            return (false, -1);

        try
        {
            lock (_lock)
            {
                var methodObject = _methodObject;
                if (methodObject is null)
                    return (false, -1);

                using var result = (ManagementBaseObject?)methodObject.InvokeMethod(methodName, [input]);
                if (result is null)
                    return (false, -1);

                var output = Convert.ToInt64(result["gmOutput"]);
                return ((output & 0xFF) == 0, output);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Acer WMI call failed. [method={methodName}, input=0x{input:X8}]", ex);
            return (false, -1);
        }
    }

    private void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initialized || _disposed)
                return;

            _initialized = true;
            try
            {
                try
                {
                    _methodObject = new ManagementObject(
                        $"{Namespace}:{MethodClass}.InstanceName=\"{PreferredInstance.Replace("\\", "\\\\")}\"");
                    _methodObject.Get();
                    return;
                }
                catch (ManagementException)
                {
                    _methodObject?.Dispose();
                    _methodObject = null;
                    // Fall through to enumeration.
                }

                using var searcher = new ManagementObjectSearcher(Namespace, $"SELECT * FROM {MethodClass}");
                using var collection = searcher.Get();
                foreach (ManagementObject instance in collection)
                {
                    if (_methodObject is null)
                        _methodObject = instance;
                    else
                        instance.Dispose();
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Acer gaming WMI interface not available.", ex);
                _methodObject?.Dispose();
                _methodObject = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _methodObject?.Dispose();
            _methodObject = null;
        }
    }
}
