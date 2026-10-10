using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Features.Msi;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features;

[Trait("Category", TestCategories.Unit)]
public class MsiHardwareFeatureTests
{
    private sealed class FakeEcChannel : IEcChannel
    {
        private readonly Dictionary<byte, byte> _ram = new();
        public bool Available { get; set; } = true;
        public List<(byte Address, byte Value)> Writes { get; } = [];

        public bool IsAvailable => Available;

        public void Seed(byte address, byte value) => _ram[address] = value;

        public bool TryRead(byte address, out byte value)
        {
            if (_ram.TryGetValue(address, out var stored))
            {
                value = stored;
                return true;
            }

            value = 0;
            return false;
        }

        public bool TryWrite(byte address, byte value)
        {
            Writes.Add((address, value));
            _ram[address] = value;
            return true;
        }
    }

    [Fact]
    public async Task MsiBatteryLimit_ShouldGetAndSet()
    {
        SetMachineInformation(MsiMachine());
        var ec = new FakeEcChannel();
        ec.Seed(0xEF, 0x50); // 80%

        var feature = new MsiBatteryChargeLimitFeature(ec);

        (await feature.IsSupportedAsync()).Should().BeTrue();
        (await feature.GetStateAsync()).Should().Be(80);

        await feature.SetStateAsync(60);
        ec.Writes.Should().Contain((0xEF, 0x3C));

        ResetCompatibilityCache();
    }

    [Fact]
    public async Task MsiCoolerBoost_ShouldToggle()
    {
        SetMachineInformation(MsiMachine());
        var ec = new FakeEcChannel();
        ec.Seed(0x98, 0x02); // Off

        var feature = new MsiCoolerBoostFeature(ec);

        (await feature.IsSupportedAsync()).Should().BeTrue();
        (await feature.GetStateAsync()).Should().BeFalse();

        await feature.SetStateAsync(true);
        ec.Writes.Should().Contain((0x98, 0x82));

        ResetCompatibilityCache();
    }

    private static MachineInformation MsiMachine() => new()
    {
        Vendor = "Micro-Star International Co., Ltd.",
        MachineType = "0000",
        Model = "MSI Raider GE78"
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
