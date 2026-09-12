using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// System-level bridge handlers: machine information, compatibility and power
/// adapter status.
/// </summary>
public static class SystemHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("system.info", async _ =>
        {
            try
            {
                var machineInformation = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
                var (isCompatible, _) = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);

                var hardware = machineInformation.Hardware is null
                    ? null
                    : await ReadHardwareAsync(machineInformation.Hardware).ConfigureAwait(false);
                var warranty = await ReadWarrantyAsync(machineInformation).ConfigureAwait(false);

                return BridgeResult.Ok(new
                {
                    vendor = machineInformation.Vendor,
                    model = machineInformation.Model,
                    machineType = machineInformation.MachineType,
                    biosVersion = machineInformation.BiosVersionRaw,
                    serialNumber = string.IsNullOrWhiteSpace(machineInformation.SerialNumber)
                        ? null
                        : machineInformation.SerialNumber,
                    isCompatible,
                    hardware,
                    warranty,
                });
            }
            catch (Exception ex)
            {
                return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
            }
        });

        // Mirrors PowerModeControl.OnRefreshAsync (Power.IsPowerAdapterConnectedAsync).
        rpc.RegisterHandler("system.powerAdapterStatus", async _ =>
        {
            try
            {
                var status = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
                return BridgeResult.Ok(new { status = status.ToString() });
            }
            catch (Exception ex)
            {
                return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
            }
        });

        // Mirrors WPF SettingsAppearanceControl / SystemTheme accent helpers.
        rpc.RegisterHandler("system.accentColor.get", async _ =>
        {
            try
            {
                var color = SystemTheme.GetAccentColor();
                await Task.CompletedTask.ConfigureAwait(false);
                return BridgeResult.Ok(new { r = color.R, g = color.G, b = color.B });
            }
            catch (Exception ex)
            {
                return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
            }
        });

        rpc.RegisterHandler("system.accentColor.set", async request =>
        {
            try
            {
                if (!TryReadByte(request.Parameters, "r", out var r) ||
                    !TryReadByte(request.Parameters, "g", out var g) ||
                    !TryReadByte(request.Parameters, "b", out var b))
                {
                    return BridgeResult.Error(-32602, "Expected parameters: r, g, b (0-255).");
                }

                SystemTheme.SetAccentColor(new RGBColor(r, g, b));
                await Task.CompletedTask.ConfigureAwait(false);
                return BridgeResult.Ok(new { applied = true });
            }
            catch (Exception ex)
            {
                return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private static bool TryReadByte(JsonElement parameters, string name, out byte value)
    {
        value = 0;
        if (!parameters.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return false;
        if (!prop.TryGetInt32(out var number) || number is < 0 or > 255)
            return false;
        value = (byte)number;
        return true;
    }

    /// <summary>
    /// Serializes the first populated hardware entries (mirrors the WPF
    /// DeviceInformationWindow formatting inputs). Fails softly: any read error
    /// yields null instead of failing the whole system.info call.
    /// </summary>
    private static async Task<object?> ReadHardwareAsync(HardwareInventory inventory)
    {
        try
        {
            var processor = inventory.Processors
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Name));
            var videoController = inventory.VideoControllers
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Name));

            var result = new
            {
                processor = processor is null ? null : new
                {
                    name = processor.Name,
                    numberOfCores = processor.NumberOfCores,
                    numberOfLogicalProcessors = processor.NumberOfLogicalProcessors,
                    maxClockSpeedMHz = processor.MaxClockSpeedMHz,
                },
                videoController = videoController is null ? null : new
                {
                    name = videoController.Name,
                    adapterCompatibility = videoController.AdapterCompatibility,
                    adapterRamBytes = videoController.AdapterRamBytes,
                },
                memory = !inventory.Memory.HasAnySignal ? null : new
                {
                    totalCapacityBytes = inventory.Memory.TotalCapacityBytes,
                    moduleCount = inventory.Memory.ModuleCount,
                    configuredClockSpeedMHz = inventory.Memory.ConfiguredClockSpeedMHz,
                    speedMHz = inventory.Memory.SpeedMHz,
                },
                baseBoard = !inventory.BaseBoard.HasAnySignal ? null : new
                {
                    manufacturer = inventory.BaseBoard.Manufacturer,
                    product = inventory.BaseBoard.Product,
                    version = inventory.BaseBoard.Version,
                },
                chassis = !inventory.Chassis.HasAnySignal ? null : new
                {
                    manufacturer = inventory.Chassis.Manufacturer,
                    chassisTypeNames = inventory.Chassis.ChassisTypeNames.ToArray(),
                },
            };

            await Task.CompletedTask.ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Hardware inventory serialization failed for system.info.", ex);
            return null;
        }
    }

    /// <summary>
    /// Looks up Lenovo warranty status (network call). Any failure or a 10
    /// second timeout yields null instead of failing the whole system.info call.
    /// </summary>
    private static async Task<object?> ReadWarrantyAsync(MachineInformation machineInformation)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var warrantyInfo = await IoCContainer.Resolve<WarrantyChecker>()
                .GetWarrantyInfo(machineInformation, token: timeout.Token)
                .ConfigureAwait(false);

            if (!warrantyInfo.HasValue)
                return null;

            return new
            {
                startDate = warrantyInfo.Value.Start?.ToString("yyyy-MM-dd"),
                endDate = warrantyInfo.Value.End?.ToString("yyyy-MM-dd"),
                link = warrantyInfo.Value.Link?.ToString(),
            };
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Warranty lookup failed for system.info.", ex);
            return null;
        }
    }
}
