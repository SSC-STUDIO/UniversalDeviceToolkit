using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class Win32
    {
        public static class ProcessStartTrace
        {
            public static IDisposable Listen(Action<int, string> handler) => WMI.Listen("root\\CIMV2",
                $"SELECT * FROM Win32_ProcessStartTrace",
                ConvertAndHandle(handler));

            public static Task<IDisposable> ListenAsync(Action<int, string> handler) => WMI.ListenAsync("root\\CIMV2",
                $"SELECT * FROM Win32_ProcessStartTrace",
                ConvertAndHandle(handler));

            private static Action<PropertyDataCollection> ConvertAndHandle(Action<int, string> handler) =>
                pdc =>
                {
                    var processId = Convert.ToInt32(pdc["ProcessID"].Value);
                    var processName = (string)pdc["ProcessName"].Value;
                    handler(processId, Path.GetFileNameWithoutExtension(processName));
                };
        }

        public static class ProcessStopTrace
        {
            public static IDisposable Listen(Action<int, string> handler) => WMI.Listen("root\\CIMV2",
                $"SELECT * FROM Win32_ProcessStopTrace",
                ConvertAndHandle(handler));

            public static Task<IDisposable> ListenAsync(Action<int, string> handler) => WMI.ListenAsync("root\\CIMV2",
                $"SELECT * FROM Win32_ProcessStopTrace",
                ConvertAndHandle(handler));

            private static Action<PropertyDataCollection> ConvertAndHandle(Action<int, string> handler) =>
                pdc =>
                {
                    var processId = Convert.ToInt32(pdc["ProcessID"].Value);
                    var processName = (string)pdc["ProcessName"].Value;
                    handler(processId, Path.GetFileNameWithoutExtension(processName));
                };
        }

        public static class ComputerSystemProduct
        {
            public static async Task<(string vendor, string name, string version, string identifyingNumber)> ReadAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT Vendor, Name, Version, IdentifyingNumber FROM Win32_ComputerSystemProduct",
                    pdc =>
                    {
                        var vendor = (string)pdc["Vendor"].Value;
                        var name = (string)pdc["Name"].Value;
                        var version = (string)pdc["Version"].Value;
                        var identifyingNumber = (string)pdc["IdentifyingNumber"].Value;
                        return (vendor, name, version, identifyingNumber);
                    }).ConfigureAwait(false);
                return result.First();
            }
        }

        public static class ComputerSystem
        {
            public static async Task<ComputerSystemHardware> ReadAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT Manufacturer, Model, SystemFamily, SystemType, ChassisSKUNumber, PCSystemType, PCSystemTypeEx FROM Win32_ComputerSystem",
                    pdc => new ComputerSystemHardware
                    {
                        Manufacturer = GetString(pdc, "Manufacturer"),
                        Model = GetString(pdc, "Model"),
                        SystemFamily = GetString(pdc, "SystemFamily"),
                        SystemType = GetString(pdc, "SystemType"),
                        ChassisSkuNumber = GetString(pdc, "ChassisSKUNumber"),
                        PcSystemType = GetNullableInt32(pdc, "PCSystemType"),
                        PcSystemTypeEx = GetNullableInt32(pdc, "PCSystemTypeEx")
                    }).ConfigureAwait(false);
                return result.FirstOrDefault() ?? ComputerSystemHardware.Empty;
            }
        }

        public static class BaseBoard
        {
            public static async Task<BaseBoardHardware> ReadAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT Manufacturer, Product, Version FROM Win32_BaseBoard",
                    pdc => new BaseBoardHardware
                    {
                        Manufacturer = GetString(pdc, "Manufacturer"),
                        Product = GetString(pdc, "Product"),
                        Version = GetString(pdc, "Version")
                    }).ConfigureAwait(false);
                return result.FirstOrDefault() ?? BaseBoardHardware.Empty;
            }
        }

        public static class SystemEnclosure
        {
            public static async Task<ChassisHardware> ReadAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT Manufacturer, ChassisTypes FROM Win32_SystemEnclosure",
                    pdc => new ChassisHardware
                    {
                        Manufacturer = GetString(pdc, "Manufacturer"),
                        ChassisTypes = GetUInt16Array(pdc, "ChassisTypes")
                    }).ConfigureAwait(false);
                return result.FirstOrDefault() ?? ChassisHardware.Empty;
            }
        }

        public static class Processor
        {
            public static async Task<string> GetNameAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT Name FROM Win32_Processor",
                    pdc => (string)pdc["Name"].Value).ConfigureAwait(false);
                return result.FirstOrDefault() ?? "Unknown CPU";
            }

            public static async Task<int> GetAddressWidthAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT AddressWidth FROM Win32_Processor",
                    pdc => Convert.ToInt32(pdc["AddressWidth"].Value)).ConfigureAwait(false);
                return result.First();
            }

            public static async Task<double> GetVoltageAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT CurrentVoltage FROM Win32_Processor",
                    pdc =>
                    {
                        if (pdc["CurrentVoltage"].Value is ushort voltageRaw)
                        {
                            if ((voltageRaw & 0x80) != 0)
                            {
                                return (voltageRaw & 0x7F) / 10.0;
                            }

                            // Without the capability bit, SMBIOS encodes the legacy voltage set
                            // rather than a raw millivolt reading. These values are not useful as
                            // live core-voltage telemetry, so treat them as unavailable.
                            return 0.0;
                        }
                        return 0.0;
                }).ConfigureAwait(false);
                return result.FirstOrDefault();
            }

            public static Task<IEnumerable<ProcessorHardware>> ReadAsync() => WMI.ReadAsync("root\\CIMV2",
                $"SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, AddressWidth, Architecture FROM Win32_Processor",
                pdc => new ProcessorHardware
                {
                    Name = GetString(pdc, "Name"),
                    Manufacturer = GetString(pdc, "Manufacturer"),
                    NumberOfCores = GetNullableInt32(pdc, "NumberOfCores"),
                    NumberOfLogicalProcessors = GetNullableInt32(pdc, "NumberOfLogicalProcessors"),
                    MaxClockSpeedMHz = GetNullableInt32(pdc, "MaxClockSpeed"),
                    AddressWidth = GetNullableInt32(pdc, "AddressWidth"),
                    Architecture = GetNullableInt32(pdc, "Architecture")
                });
        }

        public static class VideoController
        {
            public static async Task<string> GetNameAsync()
            {
                // Prioritize discrete GPU if possible, or return the first one that is not "Microsoft Basic Display Adapter"
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT Name FROM Win32_VideoController",
                    pdc => (string)pdc["Name"].Value).ConfigureAwait(false);
                
                // Simple logic: pick the one with "NVIDIA" or "AMD" or "Intel" (Arc?) if multiple
                // For now just return the first one or combine them?
                // Usually laptops have iGPU and dGPU. Users care about dGPU.
                // Or maybe I should return all unique names?
                // Let's filter for NVIDIA/AMD first.
                var discrete = result.FirstOrDefault(n => n.Contains("NVIDIA") || n.Contains("Radeon") || n.Contains("Arc"));
                return discrete ?? result.FirstOrDefault() ?? "Unknown GPU";
            }

            public static Task<IEnumerable<VideoControllerHardware>> ReadAsync() => WMI.ReadAsync("root\\CIMV2",
                $"SELECT Name, AdapterCompatibility, VideoProcessor, AdapterRAM FROM Win32_VideoController",
                pdc => new VideoControllerHardware
                {
                    Name = GetString(pdc, "Name"),
                    AdapterCompatibility = GetString(pdc, "AdapterCompatibility"),
                    VideoProcessor = GetString(pdc, "VideoProcessor"),
                    AdapterRamBytes = GetNullableUInt64(pdc, "AdapterRAM")
                });
        }

        public static class PhysicalMemory
        {
            public static Task<IEnumerable<MemoryModuleHardware>> ReadAsync() => WMI.ReadAsync("root\\CIMV2",
                $"SELECT Capacity, Manufacturer, Speed, ConfiguredClockSpeed, PartNumber FROM Win32_PhysicalMemory",
                pdc => new MemoryModuleHardware
                {
                    CapacityBytes = GetNullableUInt64(pdc, "Capacity") ?? 0,
                    Manufacturer = GetString(pdc, "Manufacturer"),
                    SpeedMHz = GetNullableInt32(pdc, "Speed"),
                    ConfiguredClockSpeedMHz = GetNullableInt32(pdc, "ConfiguredClockSpeed"),
                    PartNumber = GetString(pdc, "PartNumber")
                });
        }

        public static class Battery
        {
            public static Task<IEnumerable<BatteryHardware>> ReadAsync() => WMI.ReadAsync("root\\CIMV2",
                $"SELECT Name, Status, Chemistry, DesignCapacity, FullChargeCapacity FROM Win32_Battery",
                pdc => new BatteryHardware
                {
                    Name = GetString(pdc, "Name"),
                    Status = GetString(pdc, "Status"),
                    Chemistry = GetNullableInt32(pdc, "Chemistry"),
                    DesignCapacity = GetNullableInt32(pdc, "DesignCapacity"),
                    FullChargeCapacity = GetNullableInt32(pdc, "FullChargeCapacity")
                });
        }

        public static class OperatingSystem
        {
            public static async Task<string> GetBuildNumberAsync()
            {
                var result = await ReadAsync("root\\CIMV2",
                    $"SELECT BuildNumber FROM Win32_OperatingSystem",
                    pdc => (string)pdc["BuildNumber"].Value).ConfigureAwait(false);
                return result.First();
            }
        }

        public static class PnpEntity
        {
            public static async Task<string?> GetDeviceIDAsync(string pnpDeviceIdPart)
            {
                var results = await ReadAsync("root\\CIMV2",
                    $"SELECT * FROM Win32_PnpEntity WHERE DeviceID LIKE '{pnpDeviceIdPart}%'",
                    pdc => (string)pdc["DeviceID"].Value).ConfigureAwait(false);
                return results.FirstOrDefault();
            }
        }

        public static class PnpSignedDriver
        {
            public static Task<IEnumerable<DriverInfo>> ReadAsync() => WMI.ReadAsync("root\\CIMV2",
                $"SELECT DeviceID, HardWareID, DriverVersion, DriverDate FROM Win32_PnPSignedDriver",
                pdc =>
                {
                    var deviceId = pdc["DeviceID"].Value as string ?? string.Empty;
                    var hardwareId = pdc["HardWareId"].Value as string ?? string.Empty;
                    var driverVersionString = pdc["DriverVersion"].Value as string;
                    var driverDateString = pdc["DriverDate"].Value as string;

                    Version? driverVersion = null;
                    if (Version.TryParse(driverVersionString, out var v))
                        driverVersion = v;

                    DateTime? driverDate = null;
                    if (driverDateString is not null)
                        driverDate = ManagementDateTimeConverter.ToDateTime(driverDateString).Date;

                    return new DriverInfo(deviceId, hardwareId, driverVersion, driverDate);
                });
        }

        private static string GetString(PropertyDataCollection properties, string name) =>
            properties[name].Value as string ?? string.Empty;

        private static int? GetNullableInt32(PropertyDataCollection properties, string name) =>
            properties[name].Value is null ? null : Convert.ToInt32(properties[name].Value);

        private static ulong? GetNullableUInt64(PropertyDataCollection properties, string name) =>
            properties[name].Value is null ? null : Convert.ToUInt64(properties[name].Value);

        private static IReadOnlyCollection<ushort> GetUInt16Array(PropertyDataCollection properties, string name) =>
            properties[name].Value as ushort[] ?? [];
    }
}
