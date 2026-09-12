using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

public static partial class SensorsHandlers
{
    private static Task<BridgeResult> HandleGetSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
            var osdSettings = IoCContainer.Resolve<OsdSettings>();
            var hardwareSensorSettings = IoCContainer.Resolve<HardwareSensorSettings>();

            return Task.FromResult(BridgeResult.Ok(new
            {
                enableHardwareSensors = applicationSettings.Store.EnableHardwareSensors,
                osdRefreshIntervalSec = osdSettings.Store.OsdRefreshInterval,
                selectedGpuIsIgpu = hardwareSensorSettings.Store.SelectedGpuIsIgpu,
                showCpuAverageFrequency = hardwareSensorSettings.Store.ShowCpuAverageFrequency,
                displayMemoryInGigabytes = hardwareSensorSettings.Store.DisplayMemoryInGigabytes,
                visibleSections = hardwareSensorSettings.Store.VisibleSections,
                sectionOrder = hardwareSensorSettings.Store.SectionOrder,
            }));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static async Task<BridgeResult> HandleSetSettingsAsync(BridgeRequest request, BridgeRpcServer rpc, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hardwareSensorSettings = IoCContainer.Resolve<HardwareSensorSettings>();
            var osdSettings = IoCContainer.Resolve<OsdSettings>();

            var applicationChanged = false;
            var hardwareChanged = false;
            var osdChanged = false;

            if (TryGetBoolean(request, "enableHardwareSensors", out var enabled))
            {
                var feature = IoCContainer.Resolve<HardwareSensorsFeature>();
                await feature.SetStateAsync(enabled ? HardwareSensorsState.On : HardwareSensorsState.Off, cancellationToken).ConfigureAwait(false);
                applicationChanged = true;
            }

            if (TryGetBoolean(request, "selectedGpuIsIgpu", out var selectedGpuIsIgpu))
            {
                hardwareSensorSettings.Store.SelectedGpuIsIgpu = selectedGpuIsIgpu;
                GetSensorsGroup().SelectedGpuIsIgpu = selectedGpuIsIgpu;
                hardwareChanged = true;
            }

            if (TryGetBoolean(request, "showCpuAverageFrequency", out var showCpuAverageFrequency))
            {
                hardwareSensorSettings.Store.ShowCpuAverageFrequency = showCpuAverageFrequency;
                GetSensorsGroup().ShowAverageCpuFrequency = showCpuAverageFrequency;
                hardwareChanged = true;
            }

            if (TryGetBoolean(request, "displayMemoryInGigabytes", out var displayMemoryInGigabytes))
            {
                hardwareSensorSettings.Store.DisplayMemoryInGigabytes = displayMemoryInGigabytes;
                hardwareChanged = true;
            }

            if (TryGetDouble(request, "osdRefreshIntervalSec", out var osdRefreshIntervalSec))
            {
                osdSettings.Store.OsdRefreshInterval = Math.Clamp(osdRefreshIntervalSec, 0.1, 10);
                osdChanged = true;
            }

            if (TryGetStringArray(request, "visibleSections", out var visibleSections))
            {
                hardwareSensorSettings.Store.VisibleSections = visibleSections.Length > 0
                    ? visibleSections
                    : ["CPU", "Battery", "GPU"];
                hardwareChanged = true;
            }

            if (TryGetStringArray(request, "sectionOrder", out var sectionOrder))
            {
                hardwareSensorSettings.Store.SectionOrder = sectionOrder.Length > 0
                    ? sectionOrder
                    : ["CPU", "Battery", "GPU"];
                hardwareChanged = true;
            }

            if (hardwareChanged)
            {
                hardwareSensorSettings.SynchronizeStore();
                hardwareSensorSettings.NotifySectionsChanged();
                rpc.Publish("settings.changed", new { scope = "hardwareSensors", reason = "set" });
            }

            if (osdChanged)
            {
                osdSettings.SynchronizeStore();
                rpc.Publish("settings.changed", new { scope = "osd", reason = "set" });
            }

            if (applicationChanged)
                rpc.Publish("settings.changed", new { scope = "application", reason = "set" });

            var saved = applicationChanged || hardwareChanged || osdChanged;
            return BridgeResult.Ok(new { saved });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TryGetBoolean(BridgeRequest request, string name, out bool value)
    {
        value = false;
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetDouble(BridgeRequest request, string name, out double value)
    {
        value = 0;
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDouble(out value) ||
            !double.IsFinite(value))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetStringArray(BridgeRequest request, string name, out string[] value)
    {
        value = [];
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        value = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToArray();
        return true;
    }
}
