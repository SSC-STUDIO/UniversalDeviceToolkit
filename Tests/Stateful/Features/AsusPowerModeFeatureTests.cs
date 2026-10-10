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
public class AsusPowerModeFeatureTests
{
    private const uint RogEndpoint = 0x00120075;
    private const uint VivoEndpoint = 0x00110019;

    private sealed class FakeAtkDriver : IAsusAtkDriver
    {
        private readonly Dictionary<uint, int> _reads = new();
        public bool Available { get; set; } = true;
        public List<(uint Id, int Value)> Writes { get; } = [];
        public int SetResult { get; set; } = 1;

        public bool IsAvailable => Available;

        public void Seed(uint deviceId, int value) => _reads[deviceId] = value;

        public int DeviceGet(uint deviceId) => _reads.TryGetValue(deviceId, out var value) ? value : -1;

        public int DeviceSet(uint deviceId, int value)
        {
            Writes.Add((deviceId, value));
            if (SetResult == 1)
                _reads[deviceId] = value;
            return SetResult;
        }
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenAtkUnavailable()
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver { Available = false };
        atk.Seed(RogEndpoint, 0);

        var feature = new AsusPowerModeFeature(atk);

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonAsusMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var atk = new FakeAtkDriver();
        atk.Seed(RogEndpoint, 0);

        var feature = new AsusPowerModeFeature(atk);

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldProbeRogThenVivoEndpoint()
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(VivoEndpoint, 0); // only the vivobook endpoint responds

        var feature = new AsusPowerModeFeature(atk);

        (await feature.IsSupportedAsync()).Should().BeTrue();
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(0, PowerModeState.Balance)]
    [InlineData(1, PowerModeState.Performance)]
    [InlineData(2, PowerModeState.Quiet)]
    [InlineData(4, PowerModeState.Performance)] // manual maps onto performance for the UI
    public async Task GetState_ShouldMapRogValues(int raw, PowerModeState expected)
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(RogEndpoint, raw);

        var feature = new AsusPowerModeFeature(atk);

        (await feature.GetStateAsync()).Should().Be(expected);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(0, PowerModeState.Balance)]
    [InlineData(1, PowerModeState.Quiet)]      // vivo: 1 = silent
    [InlineData(2, PowerModeState.Performance)] // vivo: 2 = turbo
    public async Task GetState_ShouldMapVivoValuesWithSwap(int raw, PowerModeState expected)
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(VivoEndpoint, raw);

        var feature = new AsusPowerModeFeature(atk);

        (await feature.GetStateAsync()).Should().Be(expected);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 2)]
    [InlineData(PowerModeState.Balance, 0)]
    [InlineData(PowerModeState.Performance, 1)]
    public async Task SetState_ShouldWriteRogValuesAndVerifyReadBack(PowerModeState state, int expectedRaw)
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(RogEndpoint, 0);

        var feature = new AsusPowerModeFeature(atk);
        await feature.SetStateAsync(state);

        atk.Writes.Should().ContainSingle()
            .Which.Should().Be((RogEndpoint, expectedRaw));
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 1)]
    [InlineData(PowerModeState.Performance, 2)]
    public async Task SetState_ShouldWriteVivoValuesWithSwap(PowerModeState state, int expectedRaw)
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(VivoEndpoint, 0);

        var feature = new AsusPowerModeFeature(atk);
        await feature.SetStateAsync(state);

        atk.Writes.Should().ContainSingle()
            .Which.Should().Be((VivoEndpoint, expectedRaw));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldRejectGodModeAndExtreme()
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(RogEndpoint, 0);

        var feature = new AsusPowerModeFeature(atk);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.GodMode));
        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Extreme));
        atk.Writes.Should().BeEmpty();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldThrow_WhenReadBackDoesNotMatch()
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver { SetResult = 0 }; // write rejected by the device
        atk.Seed(RogEndpoint, 0);

        var feature = new AsusPowerModeFeature(atk);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Quiet));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldPreferLenovoBackend_WhenBothAvailable()
    {
        SetMachineInformation(AsusMachine()); // vendor doesn't matter here: lenovo is stubbed supported
        var atk = new FakeAtkDriver();
        atk.Seed(RogEndpoint, 0);

        var lenovo = new TestLenovoBackend(supported: true);
        var facade = new PowerModeFeature(lenovo, new AsusPowerModeFeature(atk), UnavailableHp(), UnavailableRazer(), UnavailableAlienware(), UnavailableAcer(), UnavailableMsi(), UnavailableTongfang(), UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Balance);
        lenovo.GetCalls.Should().Be(1);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldFallBackToAsusBackend_WhenLenovoUnsupported()
    {
        SetMachineInformation(AsusMachine());
        var atk = new FakeAtkDriver();
        atk.Seed(RogEndpoint, 2); // silent

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(atk),
            UnavailableHp(),
            UnavailableRazer(),
            UnavailableAlienware(),
            UnavailableAcer(),
            UnavailableMsi(),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Quiet);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldBeUnsupported_WhenNoBackendMatches()
    {
        SetMachineInformation(new MachineInformation { Vendor = "Dell Inc.", MachineType = "0000", Model = "XPS 15" });
        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new FakeAtkDriver()),
            UnavailableHp(),
            UnavailableRazer(),
            UnavailableAlienware(),
            UnavailableAcer(),
            UnavailableMsi(),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeFalse();
        await Assert.ThrowsAnyAsync<Exception>(() => facade.GetStateAsync());
        ResetCompatibilityCache();
    }

    private static MachineInformation AsusMachine() => new()
    {
        Vendor = "ASUSTeK COMPUTER INC.",
        MachineType = "0000",
        Model = "ROG Zephyrus G16"
    };

    private static HpPowerModeFeature UnavailableHp() => new(new UnavailableHpBios());

    private static RazerPowerModeFeature UnavailableRazer() => new(new UnavailableRazerHidController());

    private static AlienwarePowerModeFeature UnavailableAlienware() => new(new UnavailableAwccWmi());

    private static AcerPowerModeFeature UnavailableAcer() => new(new UnavailableAcerWmi());

    private static MsiPowerModeFeature UnavailableMsi() => new(new UnavailableEcChannel());

    private static TongfangPowerModeFeature UnavailableTongfang() => new(new UnavailableEcChannel());

    private static ClevoPowerModeFeature UnavailableClevo() => new(new UnavailableEcChannel());

    private sealed class UnavailableEcChannel : IEcChannel
    {
        public bool IsAvailable => false;
        public bool TryRead(byte address, out byte value) { value = 0; return false; }
        public bool TryWrite(byte address, byte value) => false;
    }

    private sealed class UnavailableAcerWmi : IAcerWmi
    {
        public bool IsAvailable => false;
        public (bool Ok, long Output) Execute(string methodName, uint input) => (false, -1);
    }

    private sealed class UnavailableAwccWmi : IAlienwareWmi
    {
        public bool IsAvailable => false;
        public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0) => -1;
    }

    private sealed class UnavailableRazerHidController : IRazerHidController
    {
        public bool Probe() => false;
        public int? GetPerformanceMode(byte zone) => null;
        public bool SetPerformanceMode(byte zone, byte mode, bool manualFan) => false;
        public int? GetFanRpm(byte zone) => null;
    }

    private sealed class UnavailableHpBios : IHpWmiBios
    {
        public bool IsAvailable => false;
        public (int ReturnCode, byte[] Data) Execute(uint commandType, byte[] input) => (-1, []);
    }

    private sealed class TestLenovoBackend(bool supported) : LenovoPowerModeFeature(null!, null!, null!, null!, null!)
    {
        public int GetCalls { get; private set; }

        internal override Task<bool> IsWmiSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(supported);

        internal override Task<PowerModeState> ReadStateCoreAsync(CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(PowerModeState.Balance);
        }
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
