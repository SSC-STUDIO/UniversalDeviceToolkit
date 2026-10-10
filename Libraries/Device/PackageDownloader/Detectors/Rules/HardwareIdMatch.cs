using System;
using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal static class HardwareIdMatch
{
    private static readonly string[] GenericBusNames =
    [
        "PCI", "USB", "ACPI", "HDAUDIO", "HID", "SWD", "ROOT", "DISPLAY",
        "SCSI", "IDE", "USBSTOR", "UMB", "BTH", "SD", "MMC"
    ];

    private static readonly string[] RequiresDeviceInstancePrefix =
    [
        @"PCI\", @"USB\", @"HID\", @"HDAUDIO\"
    ];

    public static bool IsSpecificHardwareId(string? hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return false;

        var id = hardwareId.Trim();
        if (id.Length < 8)
            return false;

        foreach (var busName in GenericBusNames)
        {
            if (id.Equals(busName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var prefix in RequiresDeviceInstancePrefix)
        {
            if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var hasDevice = id.Contains("&DEV_", StringComparison.OrdinalIgnoreCase);
            var hasProduct = id.Contains("&PID_", StringComparison.OrdinalIgnoreCase);
            if (!hasDevice && !hasProduct)
                return false;
        }

        return true;
    }

    public static bool Matches(string? deviceOrHardwareId, string catalogId)
    {
        if (string.IsNullOrWhiteSpace(deviceOrHardwareId) || !IsSpecificHardwareId(catalogId))
            return false;

        var catalog = catalogId.Trim();
        var device = deviceOrHardwareId.Trim();
        if (!device.StartsWith(catalog, StringComparison.OrdinalIgnoreCase))
            return false;

        if (device.Length == catalog.Length)
            return true;

        var next = device[catalog.Length];
        return next is '\\' or '/' or '&' or '#' or ',';
    }

    public static bool MatchesAny(DriverInfo driverInfo, IEnumerable<string> catalogIds)
    {
        foreach (var catalogId in catalogIds)
        {
            if (Matches(driverInfo.DeviceId, catalogId) || Matches(driverInfo.HardwareId, catalogId))
                return true;
        }

        return false;
    }
}
