using System;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class LenovoOtherMethod
    {
        public static Task<int> GetSupportThermalModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "GetSupportThermalMode",
            [],
            pdc => Convert.ToInt32(pdc["mode"].Value));

        public static Task<int> GetSupportLegionZoneVersionAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_Support_LegionZone_Version",
            [],
            pdc => Convert.ToInt32(pdc["Version"].Value));

        public static Task<int> GetLegionDeviceSupportFeatureAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_Legion_Device_Support_Feature",
            [],
            pdc => Convert.ToInt32(pdc["Status"].Value));

        public static Task<int> GetDeviceCurrentSupportFeatureAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_Device_Current_Support_Feature",
            [],
            pdc => Convert.ToInt32(pdc["Flag"].Value));

        public static Task<int> SetDeviceCurrentSupportFeatureAsync(int functionId, int value) => CallWriteRequiredAsync<int>("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Set_Device_Current_Support_Feature",
            new()
            {
                { "FunctionID", functionId },
                { "value", value }
            },
            pdc => Convert.ToInt32(pdc["ret"].Value));

        public static Task SetDGPUDeviceStatusAsync(bool status) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Set_DGPU_Device_Status",
            new() { { "Status", status ? 1 : 0 } });

        public static Task<HardwareId> GetDGPUDeviceDIDVIDAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_DGPU_Device_DIDVID",
            [],
            pdc =>
            {
                var id = Convert.ToInt32(pdc["DGPU_ID"].Value);
                var vendorId = id & 0xFFFF;
                var deviceId = id >> 16;
                return new HardwareId($"{vendorId:X}", $"{deviceId:X}");
            });

        /// <summary>
        /// Never throws. Returns -1 when unavailable so sensor polling does not spam the debugger.
        /// </summary>
        public static Task<int> GetFeatureValueAsync(CapabilityID id) => TryGetFeatureValueAsync(id);

        /// <summary>
        /// Probe-friendly feature read. Never throws; returns -1 when unavailable.
        /// Preferred for fan RPM capability IDs that many firmwares leave at 0 / unsupported.
        /// </summary>
        public static async Task<int> TryGetFeatureValueAsync(CapabilityID id)
        {
            var (ok, value) = await TryReadFeatureValueAsync(id).ConfigureAwait(false);
            return ok ? value : -1;
        }

        internal static Task<(bool Success, int Value)> TryReadFeatureValueAsync(CapabilityID id) =>
            TryCallAsync(
                "root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "GetFeatureValue",
                new() { { "IDs", (int)id } },
                pdc => Convert.ToInt32(pdc["Value"].Value),
                fallback: -1);

        public static Task SetFeatureValueAsync(CapabilityID id, int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "SetFeatureValue",
            new()
            {
                { "IDs", (int)id },
                { "value", value }
            });

        public static async Task<int> GetFeatureValueAsync(uint idRaw)
        {
            var (ok, value) = await TryCallAsync(
                "root\\WMI",
                $"SELECT * FROM LENOVO_OTHER_METHOD",
                "GetFeatureValue",
                new() { { "IDs", unchecked((int)idRaw) } },
                pdc => Convert.ToInt32(pdc["Value"].Value),
                fallback: -1).ConfigureAwait(false);
            return ok ? value : -1;
        }
        public static Task SetFeatureValueAsync(uint idRaw, int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "SetFeatureValue",
            new()
            {
                { "IDs", idRaw },
                { "value", value }
            });
    }
}
