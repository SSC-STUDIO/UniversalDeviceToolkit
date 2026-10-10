using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace UniversalDeviceToolkit.Lib.System;

public static class Battery
{
    private static ApplicationSettings? _settings;
    private static ApplicationSettings Settings => _settings ??= IoCContainer.Resolve<ApplicationSettings>();
    private static readonly object _batteryDataLock = new();
    private static int _minDischargeRate = int.MaxValue;
    private static int _maxDischargeRate = 0;
    private static double _totalTemp;
    private static int _tempSampleCount;

    public static bool IsBatteryMonitoringSupported()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Checking battery monitoring support...");

            var batteryTag = GetBatteryTag();
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery monitoring support detected, tag: {batteryTag}");
            
            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery monitoring is not supported: {ex.Message}");
            return false;
        }
    }
    
    public static void SetMinMaxDischargeRate(BATTERY_STATUS? status = null)
    {
        if (!status.HasValue)
        {
            var batteryTag = GetBatteryTag();
            status = GetBatteryStatus(batteryTag);
        }

        lock (_batteryDataLock)
        {
            if (status.Value.Rate == 0
                || (status.Value.Rate > 0 && (_minDischargeRate < 0 || _maxDischargeRate < 0))
                || (status.Value.Rate < 0 && (_minDischargeRate > 0 || _maxDischargeRate > 0)))
            {
                _minDischargeRate = int.MaxValue;
                _maxDischargeRate = 0;
            }

            if (status.Value.Rate != 0)
            {
                if (Math.Abs(status.Value.Rate) < Math.Abs(_minDischargeRate))
                    _minDischargeRate = status.Value.Rate;
                if (Math.Abs(status.Value.Rate) > Math.Abs(_maxDischargeRate))
                    _maxDischargeRate = status.Value.Rate;
            }
        }
    }

    public static BatteryInformation GetBatteryInformation()
    {
        var powerStatus = GetSystemPowerStatus();

        var batteryTag = GetBatteryTag();
        var information = GetBatteryInformation(batteryTag);
        var status = GetBatteryStatus(batteryTag);
        var modelName = GetBatteryDeviceName(batteryTag);

        double? temperatureC = null;
        DateTime? manufactureDate = null;
        DateTime? firstUseDate = null;

        SetMinMaxDischargeRate(status);

        double minDischargeRate;
        double maxDischargeRate;
        double? avgTemp;
        lock (_batteryDataLock)
        {
            try
            {
                var lenovoBatteryInformation = FindLenovoBatteryInformation();
                if (lenovoBatteryInformation.HasValue)
                {
                    temperatureC = DecodeTemperatureC(lenovoBatteryInformation.Value.Temperature);
                    if (temperatureC.HasValue)
                    {
                        _totalTemp += temperatureC.Value;
                        _tempSampleCount++;
                    }

                    manufactureDate = DecodeDateTime(lenovoBatteryInformation.Value.ManufactureDate);
                    firstUseDate = DecodeDateTime(lenovoBatteryInformation.Value.FirstUseDate);
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to get temperature of battery.", ex);
            }

            avgTemp = _tempSampleCount > 0 ? _totalTemp / _tempSampleCount : null;
            minDischargeRate = (status.Rate == 0) ? 0 : _minDischargeRate;
            maxDischargeRate = _maxDischargeRate;
        }

        return new BatteryInformation(
            powerStatus.ACLineStatus == 1,
            powerStatus.BatteryLifePercent,
            (int)powerStatus.BatteryLifeTime,
            (int)powerStatus.BatteryFullLifeTime,
            status.Rate,
            (int)minDischargeRate,
            (int)maxDischargeRate,
            (int)status.Capacity,
            (int)information.DesignedCapacity,
            (int)information.FullChargedCapacity,
            (int)information.CycleCount,
            powerStatus.ACLineStatus == 0 && information.DefaultAlert2 >= status.Capacity,
            temperatureC,
            manufactureDate,
            firstUseDate,
            modelName).WithAvgTemp(avgTemp);
    }

    private static string? GetBatteryDeviceName(uint batteryTag)
    {
        try
        {
            var queryInformation = new BATTERY_QUERY_INFORMATION
            {
                BatteryTag = batteryTag,
                InformationLevel = BATTERY_QUERY_INFORMATION_LEVEL.BatteryDeviceName,
            };

            const int bufferSize = 256;
            
            // We need to use unsafe code or a byte array to get the string because PInvokeExtensions.DeviceIoControl 
            // is generic and expects a structure for output, but here we expect a variable length string (wchar).
            // However, looking at PInvokeExtensions, there might not be an overload for byte array output easily accessible 
            // or we need to define a struct that can hold the string.
            // A common trick is to use a struct with a fixed buffer or just allocate unmanaged memory.
            // Since PInvokeExtensions wraps DeviceIoControl, let's see if we can use it.
            // The PInvokeExtensions.DeviceIoControl usually takes a struct 'out T output'.
            
            // Actually, BatteryDeviceName returns a wide string (WCHAR[]).
            // Let's use a byte array and manually call the native method if PInvokeExtensions is too restrictive,
            // OR define a struct. But string length is variable.
            
            // Let's look at existing PInvoke usage.
            // We can try to use a custom struct or just use the native PInvoke directly for this specific call 
            // to handle the buffer correctly.
            
            // Since I cannot easily modify PInvokeExtensions or see its full content, 
            // I will implement a local PInvoke call for this specific needs using Interop.
            
            // But wait, the file already uses Windows.Win32 (CsWin32 likely).
            // Let's try to use PInvoke.DeviceIoControl directly if available.
            
            unsafe
            {
                // Allocate buffer on stack or heap
                byte[] buffer = new byte[bufferSize];
                
                // Using GCHandle to pin the buffer because we cannot use fixed on a local variable if it's already considered movable by GC in some contexts,
                // but actually 'fixed' keyword is designed for this.
                // The error CS0213 "You cannot use the fixed statement to take the address of an already fixed expression" 
                // suggests that maybe 'queryInformation' is a struct on stack which is already fixed? No, struct on stack is fixed.
                // But passing address of local struct variable '&queryInformation' doesn't need 'fixed' block if it's a struct.
                // However, C# requires 'fixed' or taking address in unsafe context.
                
                // Let's simplify. We can just pass the pointer if we are in unsafe context.
                // But PInvoke call expects pointers.
                
                // Fix for CS0213: If queryInformation is a local variable of a struct type, you can take its address directly
                // without a fixed statement because it resides on the stack, which is not movable by the GC.
                
                // Fix for CS0234: System.Runtime and System.Text namespaces are missing or fully qualified name is wrong.
                // We should use 'global::System.Runtime...' or add using directives.
                
                fixed (void* outputPtr = buffer)
                {
                    // Using the raw PInvoke from Windows.Win32.PInvoke or defining a local one if needed.
                    // The 'PInvoke' class is imported.
                    
                    var handle = Devices.GetBattery();
                    if (handle.IsInvalid) return null;

                    uint bytesReturned;
                    var result = PInvoke.DeviceIoControl(
                        handle.ToWin32Handle(),
                        PInvoke.IOCTL_BATTERY_QUERY_INFORMATION,
                        &queryInformation, // Take address directly since it's on stack
                        (uint)global::System.Runtime.InteropServices.Marshal.SizeOf<BATTERY_QUERY_INFORMATION>(),
                        outputPtr,
                        bufferSize,
                        &bytesReturned,
                        null
                    );

                    if (!result) return null;

                    // The result is a WCHAR string (null terminated or not? usually null terminated or length is bytesReturned)
                    // The first few bytes might be empty or valid data.
                    // It returns a WCHAR string.
                    if (bytesReturned > 0)
                    {
                        var str = global::System.Text.Encoding.Unicode.GetString(buffer, 0, (int)bytesReturned);
                        return str.Trim('\0').Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get battery device name.", ex);
        }
        return null;
    }

    public static double? GetBatteryTemperatureC()
    {
        try
        {
            var lenovoBatteryInformation = FindLenovoBatteryInformation();
            return lenovoBatteryInformation.HasValue ? DecodeTemperatureC(lenovoBatteryInformation.Value.Temperature) : null;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get temperature of battery.", ex);
            return null;
        }
    }

    public static DateTime? GetOnBatterySince()
    {
        try
        {
            var resetOnReboot = Settings.Store.ResetBatteryOnSinceTimerOnReboot;

            var lastRebootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount);

            var logs = new List<(DateTime Date, bool IsACOnline)>();

            var query = new EventLogQuery("System", PathType.LogName, "*[System[EventID=105]]");
            using var logReader = new EventLogReader(query);
            using var propertySelector = new EventLogPropertySelector(["Event/EventData/Data[@Name='AcOnline']"]);

            while (logReader.ReadEvent() is EventLogRecord record)
            {
                var date = record.TimeCreated;
                var isAcOnline = record.GetPropertyValues(propertySelector)[0] as bool?;

                if (date is null || isAcOnline is null)
                    continue;

                if (resetOnReboot && date < lastRebootTime)
                    continue;

                logs.Add((date.Value, isAcOnline.Value));
            }

            if (logs.Count < 1)
                return null;

            logs.Reverse();

            var (dateTime, _) = logs
                .TakeWhile(log => log.IsACOnline != true)
                .LastOrDefault();

            return dateTime;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get event.", ex);
        }

        return null;
    }

    private static SYSTEM_POWER_STATUS GetSystemPowerStatus()
    {
        var result = PInvoke.GetSystemPowerStatus(out var sps);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("GetSystemPowerStatus");

        return sps;
    }

    private static uint GetBatteryTag()
    {
        var result = PInvokeExtensions.DeviceIoControl(Devices.GetBattery(),
            PInvoke.IOCTL_BATTERY_QUERY_TAG,
            0u,
            out uint tag);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, IOCTL_BATTERY_QUERY_TAG");

        return tag;
    }

    private static BATTERY_INFORMATION GetBatteryInformation(uint batteryTag)
    {
        var queryInformation = new BATTERY_QUERY_INFORMATION
        {
            BatteryTag = batteryTag,
            InformationLevel = BATTERY_QUERY_INFORMATION_LEVEL.BatteryInformation,
        };

        var result = PInvokeExtensions.DeviceIoControl(Devices.GetBattery(),
            PInvoke.IOCTL_BATTERY_QUERY_INFORMATION,
            queryInformation,
            out BATTERY_INFORMATION bi);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, IOCTL_BATTERY_QUERY_INFORMATION");
        return bi;
    }

    private static BATTERY_STATUS GetBatteryStatus(uint batteryTag)
    {
        var waitStatus = new BATTERY_WAIT_STATUS
        {
            BatteryTag = batteryTag,
        };
        var result = PInvokeExtensions.DeviceIoControl(Devices.GetBattery(),
            PInvoke.IOCTL_BATTERY_QUERY_STATUS,
            waitStatus,
            out BATTERY_STATUS s);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, IOCTL_BATTERY_QUERY_STATUS");

        return s;
    }

    private static LENOVO_BATTERY_INFORMATION? FindLenovoBatteryInformation()
    {
        for (uint index = 0; index < 3; index++)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Checking battery data at index {index}...");

            try
            {
                var info = GetLenovoBatteryInformation(index);
                if (info.Temperature is ushort.MinValue or ushort.MaxValue)
                    continue;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Battery data found at index {index}.");

                return info;
            }
            catch (Exception ex)
            {
                // Device not available or IOCTL not supported at this index.
                Log.Instance.TraceOnce(
                    $"battery-info-index-{index}",
                    $"Lenovo battery IOCTL failed at index {index}.",
                    ex);
                continue;
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Battery data not found.");

        return null;
    }

    private static LENOVO_BATTERY_INFORMATION GetLenovoBatteryInformation(uint index)
    {
        var result = PInvokeExtensions.DeviceIoControl(Drivers.GetEnergy(),
            Drivers.IOCTL_ENERGY_BATTERY_INFORMATION,
            index,
            out LENOVO_BATTERY_INFORMATION bi);
        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, 0x83102138");

        return bi;
    }

    private static DateTime? DecodeDateTime(ushort s)
    {
        try
        {
            if (s < 1)
                return null;

            var date = new DateTime((s >> 9) + 1980, (s >> 5) & 15, (s & 31), 0, 0, 0, DateTimeKind.Unspecified);
            if (date.Year is < 2018 or > 2030)
                return null;
            return date;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("battery-decode-date", $"Failed to decode battery manufacture date from value {s}.", ex);
            return null;
        }
    }

    private static double? DecodeTemperatureC(ushort s)
    {
        var value = (s - 2731.6) / 10.0;
        if (value < 0)
            return null;
        return value;
    }
}
