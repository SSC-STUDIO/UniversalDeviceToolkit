using System;
using System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Management;

/// <summary>
/// Alienware AWCC ("WMAX") WMI channel (protocol mirrors the mainline
/// alienware-wmi driver): root\wmi class AWCCWmiMethodFunction, methods take one
/// packed uint32 (operation | arg1&lt;&lt;8 | arg2&lt;&lt;16 | arg3&lt;&lt;24) and return one
/// uint32; 0xFFFFFFFF / 0xFFFFFFFE means failure. Never throws; absence of the
/// interface is reported via <see cref="IsAvailable"/>.
/// </summary>
public interface IAlienwareWmi
{
    bool IsAvailable { get; }

    /// <summary>Invokes an AWCC method; returns the uint32 result or -1 on failure/unsupported.</summary>
    int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0);
}

public sealed class AlienwareWmi : IAlienwareWmi
{
    private const string Namespace = @"root\wmi";
    private const string MethodClass = "AWCCWmiMethodFunction";

    private static readonly uint[] FailureCodes = [0xFFFFFFFF, 0xFFFFFFFE];

    private readonly object _lock = new();
    private bool _initialized;
    private ManagementObject? _methodObject;

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _methodObject is not null;
        }
    }

    public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0)
    {
        if (!IsAvailable)
            return -1;

        try
        {
            lock (_lock)
            {
                var argument = (uint)(operation | (arg1 << 8) | (arg2 << 16) | (arg3 << 24));
                using var result = (ManagementBaseObject?)_methodObject!.InvokeMethod(methodName, [argument]);
                if (result is null)
                    return -1;

                uint value;
                if (result["argr"] is not null)
                {
                    value = Convert.ToUInt32(result["argr"]);
                }
                else
                {
                    // Defensive: some firmware exposes the result under a
                    // different property name — take the first uint property.
                    value = 0;
                    var found = false;
                    foreach (var property in result.Properties)
                    {
                        if (property.Value is uint u)
                        {
                            value = u;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        return -1;
                }

                return Array.Exists(FailureCodes, code => code == value) ? -1 : (int)value;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Alienware WMI call failed. [method={methodName}, op=0x{operation:X2}]", ex);
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
                // Multiple WMAX instances may exist; use the active one.
                using var searcher = new ManagementObjectSearcher(Namespace,
                    $"SELECT * FROM {MethodClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    if (instance["Active"] is not bool active || !active)
                        continue;

                    _methodObject = instance;
                    break;
                }

                if (_methodObject is null)
                    throw new ManagementException("No active AWCC instance.");
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Alienware AWCC interface not available.", ex);
                _methodObject = null;
            }
        }
    }
}
