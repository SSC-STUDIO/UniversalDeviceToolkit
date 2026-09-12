using System;
using System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Management;

/// <summary>
/// Gigabyte GB_WMIACPI channel: the vendor MOF classes Gigabyte installs with
/// Control Center (GB_WMIACPI_Get / GB_WMIACPI_Set under root\wmi), verified
/// against the AeroCtl reverse-engineering writeup and Ixmoon's C# projects.
/// Self-disables cleanly when the MOF classes are not installed.
/// </summary>
public interface IGigabyteWmi
{
    bool IsAvailable { get; }

    /// <summary>Invokes a GB_WMIACPI_Get method; returns the numeric value or -1 on failure.</summary>
    int GetValue(string methodName);
}

public sealed class GigabyteWmi : IGigabyteWmi
{
    private const string Namespace = @"root\wmi";
    private const string GetClass = "GB_WMIACPI_Get";

    private readonly object _lock = new();
    private bool _initialized;
    private ManagementObject? _getObject;

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _getObject is not null;
        }
    }

    public int GetValue(string methodName)
    {
        if (!IsAvailable)
            return -1;

        try
        {
            lock (_lock)
            {
                using var result = (ManagementBaseObject?)_getObject!.InvokeMethod(methodName, null);
                if (result is null)
                    return -1;

                foreach (var property in result.Properties)
                {
                    if (property.Value is uint u)
                        return (int)u;
                    if (property.Value is int i)
                        return i;
                    if (property.Value is ushort us)
                        return us;
                    if (property.Value is short s)
                        return s;
                }

                return -1;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Gigabyte WMI read failed. [method={methodName}]", ex);
            return -1;
        }
    }

    private void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initialized)
                return;

            _initialized = true;
            try
            {
                using var searcher = new ManagementObjectSearcher(Namespace, $"SELECT * FROM {GetClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    _getObject = instance;
                    break;
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Gigabyte GB_WMIACPI classes not available (Control Center MOF not installed).", ex);
                _getObject = null;
            }
        }
    }
}
