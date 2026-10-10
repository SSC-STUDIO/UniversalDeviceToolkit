using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;
using ToolkitRegistry = UniversalDeviceToolkit.Lib.System.Registry;

namespace UniversalDeviceToolkit.Lib.Optimization;

internal static class WindowsOptimizationHelper
{
    public static bool AreRegistryTweaksApplied(IEnumerable<RegistryValueDefinition> tweaks)
    {
        foreach (var tweak in tweaks)
        {
            try
            {
                var currentValue = ToolkitRegistry.GetValue<object?>(tweak.Hive, tweak.SubKey, tweak.ValueName, null);
                if (currentValue is null)
                    return false;

                if (!RegistryValueEquals(currentValue, tweak.Value, tweak.Kind))
                    return false;
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    "opt-registry-tweak-read",
                    $"Failed to read optimization registry tweak {tweak.Hive}\\{tweak.SubKey}\\{tweak.ValueName}.",
                    ex);
                return false;
            }
        }

        return true;
    }

    public static bool RegistryValueEquals(object currentValue, object expectedValue, RegistryValueKind kind)
    {
        try
        {
            return kind switch
            {
                RegistryValueKind.DWord or RegistryValueKind.QWord => Convert.ToInt64(currentValue) == Convert.ToInt64(expectedValue),
                RegistryValueKind.String or RegistryValueKind.ExpandString => string.Equals(Convert.ToString(currentValue), Convert.ToString(expectedValue), StringComparison.Ordinal),
                _ => Equals(currentValue, expectedValue)
            };
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "opt-registry-value-compare",
                "Failed to compare optimization registry values.",
                ex);
            return false;
        }
    }

    public static void ApplyRegistryTweak(RegistryValueDefinition tweak)
    {
        ToolkitRegistry.SetValue(tweak.Hive, tweak.SubKey, tweak.ValueName, tweak.Value, true, tweak.Kind);
    }

    public static bool AreServicesDisabled(IEnumerable<string> services)
    {
        foreach (var serviceName in services.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var startValue = ToolkitRegistry.GetValue<int>("HKEY_LOCAL_MACHINE",
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}", "Start", -1);
                if (startValue != 4)
                    return false;
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    $"opt-service-start-{serviceName}",
                    $"Failed to read service Start value for optimization check: {serviceName}",
                    ex);
                return false;
            }

            try
            {
                using var serviceController = new ServiceController(serviceName);
                if (serviceController.Status is not ServiceControllerStatus.Stopped and not ServiceControllerStatus.StopPending)
                    return false;
            }
            catch (InvalidOperationException)
            {
                // Service not found – treat as already disabled.
            }
            catch (Exception ex)
            {
                Log.Instance.WarningOnce(
                    $"opt-service-status-{serviceName}",
                    $"Failed to query service status for optimization check: {serviceName}",
                    ex);
                return false;
            }
        }

        return true;
    }

    public static void DisableService(string serviceName)
    {
        ToolkitRegistry.SetValue("HKEY_LOCAL_MACHINE", $@"SYSTEM\CurrentControlSet\Services\{serviceName}", "Start", 4, true, RegistryValueKind.DWord);

        try
        {
            using var serviceController = new ServiceController(serviceName);

            if (serviceController.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                return;

            serviceController.Stop();
            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
        }
        catch (InvalidOperationException)
        {
            // Service not found, ignore.
        }
        catch (Exception ex)
        {
            Log.Instance.WarningOnce(
                $"opt-service-stop-{serviceName}",
                $"Failed to stop service during optimization: {serviceName}",
                ex);
        }
    }
}
