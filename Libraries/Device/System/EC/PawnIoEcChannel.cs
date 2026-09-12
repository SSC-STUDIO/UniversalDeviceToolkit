using System;
using System.Threading;
using RAMSPDToolkit.Windows.Driver;
using RAMSPDToolkit.Windows.Driver.Interfaces;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.EC;

/// <summary>
/// Embedded-controller byte access. Read path is passive; writes are reserved
/// for explicit user actions (mode switches) and always go through the global
/// EC mutex.
/// </summary>
public interface IEcChannel
{
    bool IsAvailable { get; }

    bool TryRead(byte address, out byte value);
    bool TryWrite(byte address, byte value);
}

/// <summary>
/// Standard ACPI embedded-controller transactions (cmd/status port 0x66, data
/// port 0x62, read 0x80 / write 0x81) over the PawnIO-backed
/// RAMSPDToolkit-NDD generic driver. Serialized through the Global\Access_EC
/// named mutex (same discipline as OmenMon); every failure degrades to
/// <see cref="IEcChannel.IsAvailable"/> = false instead of throwing.
/// </summary>
public sealed class PawnIoEcChannel : IEcChannel
{
    private const ushort CommandPort = 0x66;
    private const ushort DataPort = 0x62;
    private const byte CommandRead = 0x80;
    private const byte CommandWrite = 0x81;

    private const byte StatusOutputBufferFull = 0x01;
    private const byte StatusInputBufferFull = 0x02;

    private static readonly TimeSpan MutexTimeout = TimeSpan.FromMilliseconds(500);
    private const int StatusPollAttempts = 100;

    private readonly object _initLock = new();
    private bool _initialized;
    private bool _available;
    private IGenericDriver? _driver;

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public bool TryRead(byte address, out byte value)
    {
        value = 0;
        if (!IsAvailable)
            return false;

        var mutex = AcquireMutex();
        if (mutex is null)
            return false;

        try
        {
            if (!WaitForInputBufferClear())
                return false;
            _driver!.WriteIoPortByte(CommandPort, CommandRead);

            if (!WaitForInputBufferClear())
                return false;
            _driver.WriteIoPortByte(DataPort, address);

            if (!WaitForOutputBufferFull())
                return false;

            return _driver.ReadIoPortByteEx(DataPort, ref value);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"EC read failed. [address=0x{address:X2}]", ex);
            return false;
        }
        finally
        {
            ReleaseMutex(mutex);
        }
    }

    public bool TryWrite(byte address, byte value)
    {
        if (!IsAvailable)
            return false;

        var mutex = AcquireMutex();
        if (mutex is null)
            return false;

        try
        {
            if (!WaitForInputBufferClear())
                return false;
            _driver!.WriteIoPortByte(CommandPort, CommandWrite);

            if (!WaitForInputBufferClear())
                return false;
            _driver.WriteIoPortByte(DataPort, address);

            if (!WaitForInputBufferClear())
                return false;
            _driver.WriteIoPortByte(DataPort, value);

            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"EC write failed. [address=0x{address:X2}, value=0x{value:X2}]", ex);
            return false;
        }
        finally
        {
            ReleaseMutex(mutex);
        }
    }

    private void EnsureInitialized()
    {
        lock (_initLock)
        {
            if (_initialized)
                return;

            _initialized = true;
            try
            {
                if (!PawnIOHelper.IsPawnIOInstalled())
                    return;

                if (!DriverManager.LoadDriver())
                    return;

                if (DriverManager.Driver is IGenericDriver generic)
                {
                    _driver = generic;
                    _available = true;
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("EC channel initialization failed (PawnIO driver not available).", ex);
                _available = false;
                _driver = null;
            }
        }
    }

    private static Mutex? AcquireMutex()
    {
        try
        {
            var mutex = new Mutex(false, @"Global\Access_EC");
            if (!mutex.WaitOne(MutexTimeout))
            {
                mutex.Dispose();
                return null;
            }

            return mutex;
        }
        catch
        {
            return null;
        }
    }

    private static void ReleaseMutex(Mutex mutex)
    {
        try { mutex.ReleaseMutex(); }
        catch { /* ownership may vary on error paths */ }
        mutex.Dispose();
    }

    private bool WaitForInputBufferClear() => WaitForStatus(StatusInputBufferFull, 0x00);

    private bool WaitForOutputBufferFull() => WaitForStatus(StatusOutputBufferFull, StatusOutputBufferFull);

    private bool WaitForStatus(byte mask, byte expected)
    {
        for (var attempt = 0; attempt < StatusPollAttempts; attempt++)
        {
            byte status = 0;
            if (_driver!.ReadIoPortByteEx(CommandPort, ref status) && (status & mask) == expected)
                return true;

            Thread.Sleep(1);
        }

        return false;
    }
}
