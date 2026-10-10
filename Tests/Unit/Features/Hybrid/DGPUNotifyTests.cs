using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features.Hybrid;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public class DGPUNotifyTests
{
    [Fact]
    public async Task ExperimentalGpuWorkingMode_WhenToggledAfterResolve_ShouldSelectFlagsBackend()
    {
        SetMachineInformation(CreateMachineInformation(
            MachineInformation.FeatureData.SourceType.Flags,
            [CapabilityID.IGPUMode],
            supportsIGPUMode: true));

        try
        {
            var delay = new DefaultDelayProvider();
            var notify = new DGPUNotify(
                new DGPUGamezoneNotify(delay),
                new DGPUCapabilityNotify(delay),
                new DGPUFeatureFlagsNotify(delay));

            (await notify.IsSupportedAsync()).Should().BeTrue();
            notify.ResolvedBackendForTests.Should().BeOfType<DGPUGamezoneNotify>();

            notify.ExperimentalGPUWorkingMode = true;

            (await notify.IsSupportedAsync()).Should().BeTrue();
            notify.ResolvedBackendForTests.Should().BeOfType<DGPUFeatureFlagsNotify>();
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    private static MachineInformation CreateMachineInformation(
        MachineInformation.FeatureData.SourceType source,
        CapabilityID[] capabilities,
        bool supportsIGPUMode)
    {
        var features = new MachineInformation.FeatureData(source, capabilities);
        return new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83DF",
            Model = "Legion Y9000P IRX9",
            Features = features,
            Properties = new MachineInformation.PropertyData
            {
                SupportsIGPUMode = supportsIGPUMode,
            }
        };
    }

    private static void SetMachineInformation(MachineInformation machineInformation)
    {
        var lazy = new Lazy<Task<MachineInformation>>(() => Task.FromResult(machineInformation));
        typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, lazy);
    }

    private static void ResetCompatibilityCache()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        var lazyField = typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static);
        if (lazyField is null)
            return;

        var method = typeof(Compatibility).GetMethod("GetMachineInformationInternalAsync", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
            return;

        var del = Delegate.CreateDelegate(typeof(Func<Task<MachineInformation>>), method);
        var newLazy = Activator.CreateInstance(
            typeof(Lazy<Task<MachineInformation>>),
            [del, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication]);
        lazyField.SetValue(null, newLazy);
        typeof(Compatibility).GetField("_isCompatible", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    }
}
