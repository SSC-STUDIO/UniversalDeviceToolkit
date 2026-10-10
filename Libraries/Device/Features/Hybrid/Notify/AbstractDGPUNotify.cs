using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;

public abstract partial class AbstractDGPUNotify : IDGPUNotify
{
    [GeneratedRegex("pci#ven_([0-9A-Fa-f]{4})|dev_([0-9A-Fa-f]{4})")]
    private static partial Regex HardwareIdRegex();

    private readonly IDelayProvider _delayProvider;
    private readonly object _lock = new();

    protected AbstractDGPUNotify(IDelayProvider delayProvider)
    {
        _delayProvider = delayProvider;
    }

    private CancellationTokenSource? _notifyLaterCancellationTokenSource;

    public event EventHandler<bool>? Notified;

    public abstract Task<bool> IsSupportedAsync();

    public virtual void InvalidateResolution()
    {
    }

    public async Task<bool> IsDGPUAvailableAsync()
    {
        try
        {
            var dgpuHardwareId = await GetDGPUHardwareIdAsync().ConfigureAwait(false);
            if (IsHardwareIdMissing(dgpuHardwareId))
                return false;

            return IsDGPUAvailable(dgpuHardwareId);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Failed to check dGPU availability.", ex);
            return false;
        }
    }

    public async Task NotifyAsync(bool publish = true)
    {
        CancelNotifyLater();

        try
        {
            var dgpuHardwareId = await GetDGPUHardwareIdAsync().ConfigureAwait(false);
            if (IsHardwareIdMissing(dgpuHardwareId))
            {
                Log.Instance.Warning("Cannot notify dGPU status because hardware id is unavailable.");
                return;
            }

            var isAvailable = IsDGPUAvailable(dgpuHardwareId);
            await NotifyDGPUStatusAsync(isAvailable).ConfigureAwait(false);

            if (publish)
                Notified?.Invoke(this, isAvailable);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Notified: {isAvailable}");
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Failed to notify dGPU status.", ex);
        }
    }

    public Task NotifyLaterIfNeededAsync()
    {
        CancellationToken token;

        lock (_lock)
        {
            CancelNotifyLaterLocked();
            _notifyLaterCancellationTokenSource = new();

            token = _notifyLaterCancellationTokenSource.Token;
        }

        Task.Run(async () =>
        {
            try
            {
                await _delayProvider.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                await NotifyLaterAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when a newer notify request cancels this delayed callback.
            }
            catch (Exception ex)
            {
                Log.Instance.Warning("Failed to run delayed dGPU notify.", ex);
            }
        }, token).Forget("notify dGPU later after missing event");

        return Task.CompletedTask;
    }

    private async Task NotifyLaterAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event not received, notifying anyway...");

        await NotifyAsync(false).ConfigureAwait(false);
    }

    protected abstract Task NotifyDGPUStatusAsync(bool state);

    protected abstract Task<HardwareId> GetDGPUHardwareIdAsync();

    private void CancelNotifyLater()
    {
        lock (_lock)
            CancelNotifyLaterLocked();
    }

    private void CancelNotifyLaterLocked()
    {
        var previous = _notifyLaterCancellationTokenSource;
        _notifyLaterCancellationTokenSource = null;
        if (previous is null)
            return;

        try
        {
            previous.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        previous.Dispose();
    }

    internal static bool IsHardwareIdMissing(HardwareId hardwareId) =>
        string.IsNullOrEmpty(hardwareId.Vendor) || string.IsNullOrEmpty(hardwareId.Device);

    internal static bool HardwareIdsEqual(HardwareId left, HardwareId right) =>
        TryParseHexId(left.Vendor, out var leftVendor)
        && TryParseHexId(left.Device, out var leftDevice)
        && TryParseHexId(right.Vendor, out var rightVendor)
        && TryParseHexId(right.Device, out var rightDevice)
        && leftVendor == rightVendor
        && leftDevice == rightDevice;

    private static bool TryParseHexId(string? value, out int id)
    {
        id = 0;
        return !string.IsNullOrEmpty(value)
               && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
    }

    private unsafe bool IsDGPUAvailable(HardwareId dgpuHardwareId)
    {
        if (IsHardwareIdMissing(dgpuHardwareId))
            return false;

        var guidDisplayDeviceArrival = PInvoke.GUID_DISPLAY_DEVICE_ARRIVAL;
        using var deviceHandle = PInvoke.SetupDiGetClassDevs(guidDisplayDeviceArrival,
            null,
            HWND.Null,
            SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_DEVICEINTERFACE | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PROFILE);

        if (deviceHandle.IsInvalid)
            return false;

        uint index = 0;
        while (true)
        {
            var currentIndex = index;
            index++;

            var deviceInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            var result1 = PInvoke.SetupDiEnumDeviceInfo(deviceHandle, currentIndex, ref deviceInfoData);
            if (!result1)
            {
                if (Marshal.GetLastWin32Error() == PInvokeExtensions.ERROR_NO_MORE_ITEMS)
                    break;

                PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInfo");
            }

            var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
            var result2 = PInvoke.SetupDiEnumDeviceInterfaces(deviceHandle, null, guidDisplayDeviceArrival, currentIndex, ref deviceInterfaceData);
            if (!result2)
                PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInterfaces");

            var requiredSize = 0u;
            _ = PInvoke.SetupDiGetDeviceInterfaceDetail(deviceHandle.ToHdevInfo(), &deviceInterfaceData, null, 0, &requiredSize, null);

            string devicePath;
            var output = IntPtr.Zero;
            try
            {
                output = Marshal.AllocHGlobal((int)requiredSize);
                var deviceDetailData = (SP_DEVICE_INTERFACE_DETAIL_DATA_W*)output.ToPointer();
                deviceDetailData->cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DETAIL_DATA_W>();

                var result3 = PInvoke.SetupDiGetDeviceInterfaceDetail(deviceHandle.ToHdevInfo(), &deviceInterfaceData, deviceDetailData, requiredSize, null, null);
                if (!result3)
                    PInvokeExtensions.ThrowIfWin32Error("SetupDiGetDeviceInterfaceDetail");

                fixed (char* e0Ptr = &deviceDetailData->DevicePath.e0)
                    devicePath = new string(e0Ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(output);
            }

            if (!devicePath.Contains(guidDisplayDeviceArrival.ToString()))
                continue;

            if (!HardwareIdsEqual(dgpuHardwareId, HardwareIdFromDevicePath(devicePath)))
                continue;

            if (PInvoke.CM_Get_DevNode_Status(out var status, out _, deviceInfoData.DevInst, 0) != 0)
                continue;

            if (status.HasFlag(CM_DEVNODE_STATUS_FLAGS.DN_HAS_PROBLEM))
                continue;

            return true;
        }

        return false;
    }

    private static HardwareId HardwareIdFromDevicePath(string devicePath)
    {
        try
        {
            var matches = HardwareIdRegex().Matches(devicePath);
            if (matches.Count != 2)
                return default;

            var vendor = matches[0].Groups[1].Value;
            var device = matches[1].Groups[2].Value;

            return new(vendor, device);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "dgpu-hwid-from-path",
                "Failed to parse dGPU hardware id from device path.",
                ex);
            return default;
        }
    }
}
