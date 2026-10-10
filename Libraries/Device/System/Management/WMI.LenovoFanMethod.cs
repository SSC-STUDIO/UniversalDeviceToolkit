using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class LenovoFanMethod
    {
        private const string FanMethodScope = "root\\WMI";
        private const string FanMethodQuery = "SELECT * FROM LENOVO_FAN_METHOD";
        private const string FanGetCurrentFanSpeedMethod = "Fan_GetCurrentFanSpeed";

        // Out-param names used by Legion firmware (old LLT used CurrentFanSpeed only).
        private static readonly string[] FanSpeedPropertyNames =
        [
            "CurrentFanSpeed",
            "FanSpeed",
            "CurrentSpeed",
            "Speed"
        ];

        public static Task FanSetTableAsync(byte[] fanTable) => CallAsync(FanMethodScope,
            $"{FanMethodQuery}",
            "Fan_Set_Table",
            new() { { "FanTable", fanTable } });

        public static Task<bool> FanGetFullSpeedAsync() => CallAsync(FanMethodScope,
            $"{FanMethodQuery}",
            "Fan_Get_FullSpeed",
            [],
            pdc => (bool)pdc["Status"].Value);

        public static Task FanSetFullSpeedAsync(int status) => CallAsync(FanMethodScope,
            $"{FanMethodQuery}",
            "Fan_Set_FullSpeed",
            new() { { "Status", status } });

        public static async Task<int> FanGetCurrentSensorTemperatureAsync(int sensorId)
        {
            var (ok, t) = await TryCallAsync(
                FanMethodScope,
                $"{FanMethodQuery}",
                "Fan_GetCurrentSensorTemperature",
                new() { { "SensorID", sensorId } },
                pdc => Convert.ToInt32(pdc["CurrentSensorTemperature"].Value),
                fallback: -1).ConfigureAwait(false);
            return ok && t > 0 ? t : -1;
        }

        /// <summary>
        /// Historical primary fan RPM path used by LLT V1/V2 (direct FanID call).
        /// Soft-fails without throwing when the firmware method is missing.
        /// </summary>
        public static Task<int> FanGetCurrentFanSpeedAsync(int fanId) =>
            FanGetCurrentFanSpeedPreferAsync(fanId);

        /// <summary>
        /// Coordinator-friendly API: Success=false when unavailable (-1), Success=true for parked 0 or spinning RPM.
        /// </summary>
        public static async Task<(bool Success, int Rpm)> TryFanGetCurrentFanSpeedAsync(params int[] fanIds)
        {
            var rpm = await FanGetCurrentFanSpeedPreferAsync(fanIds).ConfigureAwait(false);
            return rpm < 0 ? (false, -1) : (true, rpm);
        }

        /// <summary>
        /// Try preferred fan IDs in order (V1/V2: 0/1, V3+: 1/2). First positive RPM wins.
        /// Kept deliberately small: each WMI invoke may take hundreds of ms; over-probing
        /// blew the sensor snapshot budget and froze the dashboard on cached data.
        /// </summary>
        public static async Task<int> FanGetCurrentFanSpeedPreferAsync(params int[] fanIds)
        {
            if (fanIds is null || fanIds.Length == 0)
                return -1;

            var orderedIds = fanIds.Where(id => id >= 0).Distinct().ToArray();
            if (orderedIds.Length == 0)
                return -1;

            var sawSuccessfulZero = false;

            foreach (var fanId in orderedIds)
            {
                // Old LLT signature only: FanID (int) → CurrentFanSpeed.
                var (ok, rpm) = await TryCallAsync(
                    FanMethodScope,
                    $"{FanMethodQuery}",
                    FanGetCurrentFanSpeedMethod,
                    new Dictionary<string, object> { { "FanID", fanId } },
                    ExtractFanSpeedRpm,
                    fallback: -1).ConfigureAwait(false);

                if (!ok)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Fan_GetCurrentFanSpeed unavailable for fanId={fanId}.");
                    continue;
                }

                if (rpm > 0)
                    return rpm;

                // Explicit named-property 0 = parked at this id.
                sawSuccessfulZero = true;
            }

            return sawSuccessfulZero ? 0 : -1;
        }

        /// <summary>
        /// Extract RPM only from known out-params (old LLT: CurrentFanSpeed).
        /// Never scan all properties — FanID / Status flags are often 0 and were misread
        /// as parked RPM, freezing the UI on a sticky 0 and blocking LHM fill.
        /// </summary>
        private static int ExtractFanSpeedRpm(PropertyDataCollection properties)
        {
            foreach (var name in FanSpeedPropertyNames)
            {
                try
                {
                    var raw = properties[name]?.Value;
                    if (raw is null)
                        continue;
                    var value = Convert.ToInt32(raw);
                    if (value >= 0 && value <= 100_000)
                        return value;
                }
                catch (ManagementException)
                {
                    // property not present
                }
                catch (InvalidCastException)
                {
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
            }

            throw new InvalidOperationException(
                "Fan_GetCurrentFanSpeed returned no named RPM property (CurrentFanSpeed/FanSpeed/...).");
        }

        public static async Task<int> GetCurrentFanMaxSpeedAsync(int sensorId, int fanId)
        {
            try
            {
                var result = await ReadAsync("root\\WMI",
                    $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanId}",
                    pdc => Convert.ToInt32(pdc["CurrentFanMaxSpeed"].Value)).ConfigureAwait(false);
                return result.DefaultIfEmpty(-1).Max();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ManagementException)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace(
                        $"GetCurrentFanMaxSpeed unavailable. [sensorId={sensorId}, fanId={fanId}]",
                        ex);
                return -1;
            }
        }

        public static async Task<int> GetDefaultFanMaxSpeedAsync(int sensorId, int fanID)
        {
            try
            {
                var result = await ReadAsync("root\\WMI",
                    $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanID}",
                    pdc => Convert.ToInt32(pdc["DefaultFanMaxSpeed"].Value)).ConfigureAwait(false);
                return result.DefaultIfEmpty(-1).Max();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ManagementException)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace(
                        $"GetDefaultFanMaxSpeed unavailable. [sensorId={sensorId}, fanId={fanID}]",
                        ex);
                return -1;
            }
        }
    }
}
