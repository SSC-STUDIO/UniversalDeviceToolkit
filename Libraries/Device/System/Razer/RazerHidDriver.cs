using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Razer;

/// <summary>
/// Windows HID access for Razer devices (hid.dll + setupapi.dll). All calls are
/// best-effort and never throw; every device collection is opened per call so a
/// busy interface never wedges the probe loop.
/// </summary>
public sealed class RazerHidDriver : IRazerHid
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;

    public string[] EnumerateDevicePaths(ushort vendorId)
    {
        var paths = new List<string>();

        try
        {
            HidD_GetHidGuid(out var hidGuid);
            var deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero,
                DigcfPresent | DigcfDeviceInterface);
            if (deviceInfoSet == new IntPtr(-1))
                return [];

            try
            {
                var interfaceData = new SpDeviceInterfaceData { cbSize = Marshal.SizeOf<SpDeviceInterfaceData>() };
                for (var index = 0;
                     SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData);
                     index++)
                {
                    var path = GetDevicePath(deviceInfoSet, ref interfaceData);
                    if (path is not null)
                        paths.Add(path);
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("HID enumeration failed.", ex);
        }

        return [.. paths];
    }

    public bool GetVidPid(string devicePath, out ushort vendorId, out ushort productId)
    {
        vendorId = 0;
        productId = 0;

        using var handle = OpenDevice(devicePath);
        if (handle is null)
            return false;

        var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        if (!HidD_GetAttributes(handle, ref attributes))
            return false;

        vendorId = attributes.VendorId;
        productId = attributes.ProductId;
        return true;
    }

    public bool TrySendFeatureReport(string devicePath, byte[] report)
    {
        using var handle = OpenDevice(devicePath);
        return handle is not null && HidD_SetFeature(handle, report, report.Length);
    }

    public bool TryGetFeatureReport(string devicePath, byte[] report)
    {
        using var handle = OpenDevice(devicePath);
        return handle is not null && HidD_GetFeature(handle, report, report.Length);
    }

    private static SafeFileHandle? OpenDevice(string devicePath)
    {
        var handle = CreateFile(devicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
            return null;

        return handle;
    }

    private static string? GetDevicePath(IntPtr deviceInfoSet, ref SpDeviceInterfaceData interfaceData)
    {
        var detailData = new SpDeviceInterfaceDetailData
        {
            cbSize = IntPtr.Size == 8 ? 8 : (4 + 1),
        };

        return SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, ref detailData,
                Marshal.SizeOf<SpDeviceInterfaceDetailData>(), out _, IntPtr.Zero)
            ? detailData.DevicePath
            : null;
    }

    private const int DigcfPresent = 0x2;
    private const int DigcfDeviceInterface = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SpDeviceInterfaceDetailData
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, [In] byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetFeature(SafeFileHandle hidDeviceObject, [In, Out] byte[] reportBuffer, int reportBufferLength);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string? enumerator, IntPtr hwndParent, int flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
        ref Guid interfaceClassGuid, int memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData, ref SpDeviceInterfaceDetailData deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
}
