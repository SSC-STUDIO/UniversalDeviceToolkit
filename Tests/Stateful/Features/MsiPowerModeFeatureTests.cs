using System.Reflection;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Acer;
using UniversalDeviceToolkit.Lib.Features.Asus;
using UniversalDeviceToolkit.Lib.Features.Clevo;
using UniversalDeviceToolkit.Lib.Features.Dell;
using UniversalDeviceToolkit.Lib.Features.Hp;
using UniversalDeviceToolkit.Lib.Features.Msi;
using UniversalDeviceToolkit.Lib.Features.Razer;
using UniversalDeviceToolkit.Lib.Features.Tongfang;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.System.Razer;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features;

[Trait("Category", TestCategories.Unit)]
[Collection("PowerModeFeatureTests")]
public class MsiPowerModeFeatureTests
{
    private sealed class FakeEcChannel : IEcChannel
    {
        private readonly Dictionary<byte, byte> _ram = new();
        public bool Available { get; set; } = true;
        public List<(byte Address, byte Value)> Writes { get; } = [];
        public bool WriteSucceeds { get; set; } = true;

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
            if (!WriteSucceeds)
                return false;

            _ram[address] = value;
            return true;
        }
    }

    private static FakeEcChannel Gen2Ec(byte shiftMode = 0xC1)
    {
        var ec = new FakeEcChannel();
        ec.Seed(0xD2, shiftMode);
        return ec;
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenEcUnavailable()
    {
        SetMachineInformation(MsiMachine());
        var feature = new MsiPowerModeFeature(new FakeEcChannel { Available = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonMsiMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new MsiPowerModeFeature(Gen2Ec());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldDetectGen2Layout()
    {
        SetMachineInformation(MsiMachine());
        var feature = new MsiPowerModeFeature(Gen2Ec(0xC0));

        (await feature.IsSupportedAsync()).Should().BeTrue();
        (await feature.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldFallBackToGen1Layout()
    {
        SetMachineInformation(MsiMachine());
        var ec = new FakeEcChannel();
        ec.Seed(0xF2, 0xC1);

        var feature = new MsiPowerModeFeature(ec);

        (await feature.IsSupportedAsync()).Should().BeTrue();
        (await feature.GetStateAsync()).Should().Be(PowerModeState.Balance);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenNoLayoutMatches()
    {
        SetMachineInformation(MsiMachine());
        var feature = new MsiPowerModeFeature(new FakeEcChannel()); // empty RAM

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0xC2)]
    [InlineData(PowerModeState.Balance, 0xC1)]
    [InlineData(PowerModeState.Performance, 0xC0)]
    public async Task SetState_ShouldWriteAndVerifyReadBack(PowerModeState state, byte expectedMode)
    {
        SetMachineInformation(MsiMachine());
        var ec = Gen2Ec();

        var feature = new MsiPowerModeFeature(ec);
        await feature.SetStateAsync(state);

        ec.Writes.Should().ContainSingle()
            .Which.Should().Be((0xD2, expectedMode));
        (await feature.GetStateAsync()).Should().Be(state);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldThrow_WhenWriteFails()
    {
        SetMachineInformation(MsiMachine());
        var ec = Gen2Ec();
        ec.WriteSucceeds = false;

        var feature = new MsiPowerModeFeature(ec);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Quiet));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldRejectGodModeAndExtreme()
    {
        SetMachineInformation(MsiMachine());
        var feature = new MsiPowerModeFeature(Gen2Ec());

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.GodMode));
        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Extreme));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldUseMsiBackend_WhenEarlierBackendsUnsupported()
    {
        SetMachineInformation(MsiMachine());

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new UnavailableAtk()),
            new HpPowerModeFeature(new UnavailableHpBios()),
            new RazerPowerModeFeature(new UnavailableRazerHidController()),
            UnavailableAlienware(),
            UnavailableAcer(),
            new MsiPowerModeFeature(Gen2Ec(0xC0)),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    private static MachineInformation MsiMachine() => new()
    {
        Vendor = "Micro-Star International Co., Ltd.",
        MachineType = "0000",
        Model = "MSI Raider 18"
    };

    private static AlienwarePowerModeFeature UnavailableAlienware() => new(new UnavailableAwccWmi());

    private static TongfangPowerModeFeature UnavailableTongfang() => new(new FakeEcChannel { Available = false });

    private static ClevoPowerModeFeature UnavailableClevo() => new(new FakeEcChannel { Available = false });

    private sealed class UnavailableAwccWmi : IAlienwareWmi
    {
        public bool IsAvailable => false;
        public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0) => -1;
    }

    private static AcerPowerModeFeature UnavailableAcer() => new(new UnavailableAcerWmi());

    private sealed class UnavailableAcerWmi : IAcerWmi
    {
        public bool IsAvailable => false;
        public (bool Ok, long Output) Execute(string methodName, uint input) => (false, -1);
    }

    private sealed class UnavailableAtk : IAsusAtkDriver
    {
        public bool IsAvailable => false;
        public int DeviceGet(uint deviceId) => -1;
        public int DeviceSet(uint deviceId, int value) => -1;
    }

    private sealed class UnavailableHpBios : IHpWmiBios
    {
        public bool IsAvailable => false;
        public (int ReturnCode, byte[] Data) Execute(uint commandType, byte[] input) => (-1, []);
    }

    private sealed class UnavailableRazerHidController : IRazerHidController
    {
        public bool Probe() => false;
        public int? GetPerformanceMode(byte zone) => null;
        public bool SetPerformanceMode(byte zone, byte mode, bool manualFan) => false;
        public int? GetFanRpm(byte zone) => null;
    }

    private sealed class TestLenovoBackend(bool supported) : LenovoPowerModeFeature(null!, null!, null!, null!, null!)
    {
        internal override Task<bool> IsWmiSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(supported);

        internal override Task<PowerModeState> ReadStateCoreAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PowerModeState.Balance);
    }

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
