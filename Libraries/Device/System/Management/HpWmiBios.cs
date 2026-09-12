using System;
using System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Management;

/// <summary>
/// HP WMI BIOS channel (protocol mirrors OmenMon's Bios.cs and the Linux
/// hp-wmi driver): root\wmi class hpqBIntM instance "ACPI\PNP0C14\0_0",
/// method hpqBIOSInt128, signed with "SECU" on the default command channel.
/// Never throws; absence of the interface is reported via <see cref="IsAvailable"/>.
/// </summary>
public interface IHpWmiBios
{
    bool IsAvailable { get; }

    /// <summary>Executes a command on the default (gaming) channel. Returns (rwReturnCode, payload); rc 0 = success, -1 = client-side failure.</summary>
    (int ReturnCode, byte[] Data) Execute(uint commandType, byte[] input);
}

public sealed class HpWmiBios : IHpWmiBios, IDisposable
{
    private const string Namespace = @"root\wmi";
    private const string MethodClass = "hpqBIntM";
    private const string MethodInstance = @"ACPI\PNP0C14\0_0";
    private const string DataInClass = "hpqBDataIn";
    private const string MethodName = "hpqBIOSInt128";
    private const uint CommandDefault = 0x20008;
    private static readonly byte[] Signature = { 0x53, 0x45, 0x43, 0x55 }; // "SECU"

    private readonly object _lock = new();
    private bool _initialized;
    private bool _disposed;
    private ManagementObject? _methodObject;
    private ManagementClass? _dataInClass;

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _methodObject is not null && _dataInClass is not null;
        }
    }

    public (int ReturnCode, byte[] Data) Execute(uint commandType, byte[] input)
    {
        if (!IsAvailable)
            return (-1, []);

        try
        {
            lock (_lock)
            {
                var dataInClass = _dataInClass;
                var methodObject = _methodObject;
                if (dataInClass is null || methodObject is null)
                    return (-1, []);

                using var inData = dataInClass.CreateInstance();
                if (inData is null)
                    return (-1, []);

                inData["Sign"] = Signature;
                inData["Command"] = CommandDefault;
                inData["CommandType"] = commandType;
                inData["Size"] = (uint)input.Length;
                inData["Data"] = input;

                using var outParams = (ManagementBaseObject?)methodObject.InvokeMethod(MethodName, [inData]);
                if (outParams is null)
                    return (-1, []);

                var returnCode = -1;
                byte[] data = [];

                if (outParams["rwReturnCode"] is not null)
                    returnCode = Convert.ToInt32(outParams["rwReturnCode"]);

                if (outParams["OutData"] is ManagementBaseObject outData && outData["Data"] is byte[] payload)
                    data = payload;

                return (returnCode, data);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"HP WMI BIOS call failed. [commandType=0x{commandType:X2}]", ex);
            return (-1, []);
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
                _methodObject = new ManagementObject(
                    $"{Namespace}:{MethodClass}.InstanceName=\"{MethodInstance.Replace("\\", "\\\\")}\"");
                _methodObject.Get();
                _dataInClass = new ManagementClass($"{Namespace}:{DataInClass}");
                _dataInClass.Get();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("HP WMI BIOS interface not available.", ex);
                _methodObject?.Dispose();
                _dataInClass?.Dispose();
                _methodObject = null;
                _dataInClass = null;
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
            _dataInClass?.Dispose();
            _methodObject = null;
            _dataInClass = null;
        }
    }
}
