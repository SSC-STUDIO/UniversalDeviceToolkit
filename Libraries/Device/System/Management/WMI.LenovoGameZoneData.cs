using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    public static partial class LenovoGameZoneData
    {
        [GeneratedRegex("PCIVEN_([0-9A-F]{4})|DEV_([0-9A-F]{4})")]
        private static partial Regex DGPUHWIdRegex();

        [GeneratedRegex(@"PCI\\VEN_([0-9A-F]{4})|DEV_([0-9A-F]{4})")]
        private static partial Regex DGPUHWIdRegexV2();

        public static Task<bool> ExistsAsync() => WMI.ExistsAsync("root\\WMI", $"SELECT * FROM LENOVO_GAMEZONE_DATA");

        public static Task<(bool Success, int Value)> TryGetFanCountAsync() =>
            TryCallAsync(
                "root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                "GetFanCount",
                [],
                pdc => Convert.ToInt32(pdc["Data"].Value),
                fallback: -1);

        public static Task<(bool Success, int Value)> TryGetFan1SpeedAsync() =>
            TryGetFanSpeedAsync("GetFan1Speed");

        public static Task<(bool Success, int Value)> TryGetFan2SpeedAsync() =>
            TryGetFanSpeedAsync("GetFan2Speed");

        private static Task<(bool Success, int Value)> TryGetFanSpeedAsync(string methodName) =>
            TryCallAsync(
                "root\\WMI",
                $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                methodName,
                [],
                pdc => Convert.ToInt32(pdc["Data"].Value),
                fallback: -1);

        internal static async Task<(bool Success, int Rpm)> TryGetCpuFanSpeedAsync()
        {
            var (countSuccess, count) = await TryGetFanCountAsync().ConfigureAwait(false);
            return countSuccess && count >= 1
                ? await TryGetFan1SpeedAsync().ConfigureAwait(false)
                : (false, -1);
        }

        internal static async Task<(bool Success, int Rpm)> TryGetGpuFanSpeedAsync()
        {
            var (countSuccess, count) = await TryGetFanCountAsync().ConfigureAwait(false);
            return countSuccess && count >= 2
                ? await TryGetFan2SpeedAsync().ConfigureAwait(false)
                : (false, -1);
        }

        public static Task<int> GetBIOSOCMode() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetBIOSOCMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> IsSupportSmartFanAsync() => CallGameZoneMethodWithCimFallbackAsync("IsSupportSmartFan", []);

        public static Task<int> GetSmartFanModeAsync() => CallGameZoneMethodWithCimFallbackAsync("GetSmartFanMode", []);

        public static Task SetSmartFanModeAsync(int data) => WriteGameZoneMethodWithCimFallbackAsync(
            "SetSmartFanMode",
            new() { { "Data", data } },
            data);

        private static Task<int> CallGameZoneMethodWithCimFallbackAsync(
            string methodName,
            Dictionary<string, object> methodParams) =>
            ResolveGameZoneSupportWithCimFallbackAsync(
                methodName,
                async () =>
                {
                    var (success, value) = await TryReadGameZoneDataAsync(methodName, methodParams).ConfigureAwait(false);
                    return success ? value : null;
                },
                async () => await InvokeGameZoneMethodViaCimProcessAsync(methodName).ConfigureAwait(false));

        internal static async Task<int> ResolveGameZoneSupportWithCimFallbackAsync(
            string methodName,
            Func<Task<int?>> classicReader,
            Func<Task<int?>> cimReader)
        {
            int? classic = null;
            try
            {
                classic = await classicReader().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    $"wmi-gamezone-classic-fallback-{methodName}",
                    $"Classic GameZone {methodName} fallback probe failed.",
                    ex);
            }

            // Support/mode probes are always >= 1; null, zero, or a failed classic
            // invoke means empty System.Management out-parameters (Y9000P IRX9)
            // or a real "no", either of which must be confirmed through CIM.
            if (classic is > 0)
                return classic.Value;

            try
            {
                var cim = await cimReader().ConfigureAwait(false);
                return cim is > 0 ? cim.Value : 0;
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    $"wmi-gamezone-cim-fallback-{methodName}",
                    $"CIM GameZone {methodName} fallback probe failed.",
                    ex);
                return 0;
            }
        }

        /// <summary>
        /// Reads a GameZone integer where 0 is a valid state (GSync Off, iGPU Default).
        /// Falls back to CIM only when classic System.Management did not marshal Data.
        /// </summary>
        private static async Task<int> CallGameZoneStateWithCimFallbackAsync(string methodName)
        {
            var (ok, classic) = await TryReadGameZoneDataAsync(methodName, []).ConfigureAwait(false);
            if (ok)
                return classic;

            return await InvokeGameZoneMethodViaCimProcessAsync(methodName).ConfigureAwait(false);
        }

        private static async Task<(bool Success, int Value)> TryReadGameZoneDataAsync(
            string methodName,
            Dictionary<string, object> methodParams)
        {
            try
            {
                return await TryCallAsync(
                    "root\\WMI",
                    $"SELECT * FROM LENOVO_GAMEZONE_DATA",
                    methodName,
                    methodParams,
                    ConvertGameZoneData,
                    fallback: 0).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    $"wmi-gamezone-classic-{methodName}",
                    $"Classic GameZone {methodName} probe failed.",
                    ex);
                return (false, 0);
            }
        }

        private static int ConvertGameZoneData(PropertyDataCollection pdc)
        {
            foreach (PropertyData property in pdc)
            {
                if (property.Name.Equals("Data", StringComparison.OrdinalIgnoreCase) && property.Value is not null)
                    return Convert.ToInt32(property.Value);
            }

            throw new InvalidOperationException("GameZone Data out-parameter is empty.");
        }

        private static async Task WriteGameZoneMethodWithCimFallbackAsync(
            string methodName,
            Dictionary<string, object> methodParams,
            int value,
            string cimParameterName = "Data")
        {
            FormattableString query = $"SELECT * FROM LENOVO_GAMEZONE_DATA";
            var result = await CallWriteSequenceAsync(
                "root\\WMI",
                query,
                methodName,
                methodParams,
                classicResult => ResolveGameZoneWriteWithCimFallbackAsync(
                    () => Task.FromResult(classicResult),
                    () => InvokeGameZoneWriteViaCimProcessAsync(
                        methodName,
                        value,
                        cimParameterName))).ConfigureAwait(false);

            result.ThrowIfNotSucceeded(
                "root\\WMI",
                query.ToString(WMIPropertyValueFormatter.Instance),
                methodName,
                _wmiInvokeTimeoutMs);
        }

        internal static async Task<WmiWriteResult> ResolveGameZoneWriteWithCimFallbackAsync(
            Func<Task<WmiWriteResult>> classicWriter,
            Func<Task<WmiWriteResult>> cimWriter)
        {
            ArgumentNullException.ThrowIfNull(classicWriter);
            ArgumentNullException.ThrowIfNull(cimWriter);

            var classicResult = await classicWriter().ConfigureAwait(false);
            if (classicResult.Status != WmiWriteStatus.Unavailable)
                return classicResult;

            return await cimWriter().ConfigureAwait(false);
        }

        public static Task<int> GetIntelligentSubModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetIntelligentSubMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task SetIntelligentSubModeAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetIntelligentSubMode",
            new() { { "Data", data } });

        public static Task<int> IsSupportGSyncAsync() => CallGameZoneMethodWithCimFallbackAsync("IsSupportGSync", []);

        public static Task<int> GetGSyncStatusAsync() => CallGameZoneStateWithCimFallbackAsync("GetGSyncStatus");

        public static Task SetGSyncStatusAsync(int data) => WriteGameZoneMethodWithCimFallbackAsync(
            "SetGSyncStatus",
            new() { { "Data", data } },
            data);

        public static Task<int> IsSupportIGPUModeAsync() => CallGameZoneMethodWithCimFallbackAsync("IsSupportIGPUMode", []);

        public static Task<int> GetIGPUModeStatusAsync() => CallGameZoneStateWithCimFallbackAsync("GetIGPUModeStatus");

        public static Task SetIGPUModeStatusAsync(int mode) => WriteGameZoneMethodWithCimFallbackAsync(
            "SetIGPUModeStatus",
            new() { { "mode", mode } },
            mode,
            "mode");

        public static Task NotifyDGPUStatusAsync(int status) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "NotifyDGPUStatus",
            new() { { "Status", status } });

        public static Task<HardwareId> GetDGPUHWIdAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetDGPUHWId",
            [],
            pdc =>
            {
                var id = pdc["Data"].Value.ToString();

                if (id is null)
                    return HardwareId.Empty;

                try
                {
                    var matches = DGPUHWIdRegex().Matches(id);
                    if (matches.Count != 2)
                    {
                        matches = DGPUHWIdRegexV2().Matches(id);
                        if (matches.Count != 2)
                            return HardwareId.Empty;
                    }

                    var vendor = matches[0].Groups[1].Value;
                    var device = matches[1].Groups[2].Value;

                    return new HardwareId(vendor, device);
                }
                catch (Exception ex)
                {
                    Log.Instance.TraceOnce(
                        "wmi-gamezone-dgpu-hwid-parse",
                        "Failed to parse dGPU hardware id from GameZone WMI.",
                        ex);
                    return HardwareId.Empty;
                }
            });

        public static Task<(bool Success, int Value)> TryIsSupportGpuOCAsync() => TryCallAsync(
            "ROOT\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportGpuOC",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value),
            fallback: 0);

        public static Task<int> IsSupportGpuOCAsync() => CallAsync("ROOT\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportGpuOC",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> IsSupportDisableTPAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportDisableTP",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> GetTPStatusStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetTPStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task SetTPStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetTPStatus",
            new() { { "Data", data } });

        public static Task<int> IsSupportDisableWinKeyAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportDisableWinKey",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> GetWinKeyStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetWinKeyStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task SetWinKeyStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetWinKeyStatus",
            new() { { "Data", data } });

        public static Task<int> IsSupportODAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportOD",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> GetODStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetODStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task SetODStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetODStatus",
            new() { { "Data", data } });

        public static Task SetLightControlOwnerAsync(int data) => CallAsync("ROOT\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetLightControlOwner",
            new() { { "Data", data } });

        public static Task<int> IsACFitForOCAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsACFitForOC",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> GetPowerChargeModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetPowerChargeMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        public static Task<int> GetCPUFrequencyAsync() => WMI.CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetCPUFrequency",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["Data"].Value);
                var low = value & 0xFFFF;
                var high = value >> 16;
                return Math.Max(low, high);
            });
    }
}
