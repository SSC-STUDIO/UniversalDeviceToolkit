using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Features.Asus;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features;

[Trait("Category", TestCategories.Unit)]
public class AsusBatteryChargeLimitFeatureTests
{
    private sealed class FakeAtkDriver : IAsusAtkDriver
    {
        public bool Available { get; set; } = true;
        public int Threshold { get; set; } = 80;
        public int LastSetDevice { get; private set; }
        public int LastSetValue { get; private set; }

        public bool IsAvailable => Available;

        public int DeviceGet(uint deviceId)
        {
            if (!Available || deviceId != 0x00120057)
                return -1;

            return Threshold;
        }

        public int DeviceSet(uint deviceId, int value)
        {
            if (!Available || deviceId != 0x00120057)
                return -1;

            LastSetDevice = (int)deviceId;
            LastSetValue = value;
            Threshold = value;
            return 1;
        }
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenAtkUnavailable()
    {
        SetMachineInformation(AsusMachine());
        var feature = new AsusBatteryChargeLimitFeature(new FakeAtkDriver { Available = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonAsusMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new AsusBatteryChargeLimitFeature(new FakeAtkDriver());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task GetState_ShouldReturnThreshold()
    {
        SetMachineInformation(AsusMachine());
        var feature = new AsusBatteryChargeLimitFeature(new FakeAtkDriver { Threshold = 80 });

        (await feature.IsSupportedAsync()).Should().BeTrue();
        (await feature.GetStateAsync()).Should().Be(80);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldUpdateThreshold()
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver { Threshold = 100 };
        var feature = new AsusBatteryChargeLimitFeature(atk);

        await feature.SetStateAsync(80);

        atk.LastSetValue.Should().Be(80);
        (await feature.GetStateAsync()).Should().Be(80);
        ResetCompatibilityCache();
    }

    private static MachineInformation AsusMachine() => new()
    {
        Vendor = "ASUSTeK COMPUTER INC.",
        MachineType = "0000",
        Model = "ROG Strix G16"
    };

    private static void SetMachineInformation(MachineInformation machineInformation)
    {
        var lazy = new Lazy<Task<MachineInformation>>(() => Task.FromResult(machineInformation));
        typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, lazy);
    }

    private static void ResetCompatibilityCache()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        var lazyField = typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static);
        if (lazyField != null)
        {
            var method = typeof(Compatibility).GetMethod("GetMachineInformationInternalAsync", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                var del = Delegate.CreateDelegate(typeof(Func<Task<MachineInformation>>), method);
                var newLazy = Activator.CreateInstance(typeof(Lazy<Task<MachineInformation>>), [del, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication]);
                lazyField.SetValue(null, newLazy);
            }
        }
        typeof(Compatibility).GetField("_isCompatible", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    }
}
