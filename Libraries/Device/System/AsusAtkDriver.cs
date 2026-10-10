using System;
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System;

/// <summary>
/// Low-level ASUS ATKACPI driver channel (same protocol as G-Helper's AsusACPI
/// and the Linux asus-wmi driver). All calls are best-effort and throw nothing;
/// absence of the driver/device is reported via <see cref="IAsusAtkDriver.IsAvailable"/>.
/// </summary>
public interface IAsusAtkDriver
{
    bool IsAvailable { get; }

    /// <summary>DSTS read. Returns DeviceGet(devId) semantics: int32(result) - 0x10000; negative means unsupported.</summary>
    int DeviceGet(uint deviceId);

    /// <summary>DEVS write of a single u32 value. Returns the raw result (1 = success for most endpoints).</summary>
    int DeviceSet(uint deviceId, int value);
}

public sealed class AsusAtkDriver : IAsusAtkDriver
{
    private const string DevicePath = @"\\.\ATKACPI";
    private const uint ControlCode = 0x0022240C;

    private const uint MethodDsts = 0x53545344; // "DSTS"
    private const uint MethodDevs = 0x53564544; // "DEVS"

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x80;

    private readonly object _lock = new();
    private IntPtr _handle = IntPtr.Zero;
    private bool _initialized;
    private bool _available;

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public int DeviceGet(uint deviceId)
    {
        if (!IsAvailable)
            return -1;

        try
        {
            // G-Helper: args = 8 bytes, only arg0 = deviceId.
            var args = new byte[8];
            BitConverter.GetBytes(deviceId).CopyTo(args, 0);
            var status = CallMethod(MethodDsts, args);
            if (status is null || status.Length < 4)
                return -1;

            return BitConverter.ToInt32(status, 0) - 65536;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"ATK DeviceGet failed. [deviceId=0x{deviceId:X8}]", ex);
            return -1;
        }
    }

    public int DeviceSet(uint deviceId, int value)
    {
        if (!IsAvailable)
            return -1;

        try
        {
            var args = new byte[8];
            BitConverter.GetBytes(deviceId).CopyTo(args, 0);
            BitConverter.GetBytes(value).CopyTo(args, 4);
            var status = CallMethod(MethodDevs, args);
            if (status is null || status.Length < 4)
                return -1;

            return BitConverter.ToInt32(status, 0);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"ATK DeviceSet failed. [deviceId=0x{deviceId:X8}, value={value}]", ex);
            return -1;
        }
    }

    public bool IsSupported(uint deviceId) => DeviceGet(deviceId) >= 0;

    private byte[]? CallMethod(uint methodId, byte[] args)
    {
        var acpiBuffer = new byte[8 + args.Length];
        BitConverter.GetBytes(methodId).CopyTo(acpiBuffer, 0);
        BitConverter.GetBytes((uint)args.Length).CopyTo(acpiBuffer, 4);
        Array.Copy(args, 0, acpiBuffer, 8, args.Length);

        var outBuffer = new byte[16];
        uint bytesReturned = 0;

        lock (_lock)
        {
            var ok = DeviceIoControl(
                _handle,
                ControlCode,
                acpiBuffer,
                (uint)acpiBuffer.Length,
                outBuffer,
                (uint)outBuffer.Length,
                ref bytesReturned,
                IntPtr.Zero);

            return ok ? outBuffer : null;
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
                _handle = CreateFile(
                    DevicePath,
                    GenericRead | GenericWrite,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    FileAttributeNormal,
                    IntPtr.Zero);

                _available = _handle != new IntPtr(-1) && _handle != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("ATKACPI driver not available.", ex);
                _available = false;
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        ref uint lpBytesReturned,
        IntPtr lpOverlapped);
}
