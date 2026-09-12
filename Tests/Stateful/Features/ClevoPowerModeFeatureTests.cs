using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
public class ClevoPowerModeFeatureTests
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

    private static FakeEcChannel ClevoEc(byte mode = 0x00)
    {
        var ec = new FakeEcChannel();
        ec.Seed(0xD8, mode);
        return ec;
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenEcUnavailable()
    {
        SetMachineInformation(ClevoMachine());
        var feature = new ClevoPowerModeFeature(new FakeEcChannel { Available = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonClevoMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new ClevoPowerModeFeature(ClevoEc());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldDetectLayoutAndReadState()
    {
        SetMachineInformation(ClevoMachine());
        var feature = new ClevoPowerModeFeature(ClevoEc(0x03));

        (await feature.IsSupportedAsync()).Should().BeTrue();
        (await feature.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0x01)]
    [InlineData(PowerModeState.Balance, 0x00)]
    [InlineData(PowerModeState.Performance, 0x03)]
    public async Task SetState_ShouldWriteAndVerifyReadBack(PowerModeState state, byte expectedValue)
    {
        SetMachineInformation(ClevoMachine());
        var ec = ClevoEc();

        var feature = new ClevoPowerModeFeature(ec);
        await feature.SetStateAsync(state);

        ec.Writes.Should().ContainSingle()
            .Which.Should().Be((0xD8, expectedValue));
        (await feature.GetStateAsync()).Should().Be(state);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldUseClevoBackend_WhenEarlierBackendsUnsupported()
    {
        SetMachineInformation(ClevoMachine());

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new UnavailableAtk()),
            new HpPowerModeFeature(new UnavailableHpBios()),
            new RazerPowerModeFeature(new UnavailableRazerHidController()),
            new AlienwarePowerModeFeature(new UnavailableAwccWmi()),
            new AcerPowerModeFeature(new UnavailableAcerWmi()),
            new MsiPowerModeFeature(new FakeEcChannel { Available = false }),
            new TongfangPowerModeFeature(new FakeEcChannel { Available = false }),
            new ClevoPowerModeFeature(ClevoEc(0x03)));

        (await facade.IsSupportedAsync()).Should().BeTrue();
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    private static MachineInformation ClevoMachine() => new()
    {
        Vendor = "CLEVO",
        MachineType = "0000",
        Model = "NH50"
    };

    private sealed class UnavailableAwccWmi : IAlienwareWmi
    {
        public bool IsAvailable => false;
        public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0) => -1;
    }

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
        internal override Task<bool> IsWmiSupportedAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(supported);

        internal override Task<PowerModeState> ReadStateCoreAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult(PowerModeState.Balance);
    }

    private static void SetMachineInformation(MachineInformation machineInformation)
    {
        var lazy = new System.Lazy<Task<MachineInformation>>(() => Task.FromResult(machineInformation));
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
                var del = System.Delegate.CreateDelegate(typeof(System.Func<Task<MachineInformation>>), method);
                var newLazy = System.Activator.CreateInstance(typeof(System.Lazy<Task<MachineInformation>>), [del, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication]);
                lazyField.SetValue(null, newLazy);
            }
        }
        typeof(Compatibility).GetField("_isCompatible", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    }
}
