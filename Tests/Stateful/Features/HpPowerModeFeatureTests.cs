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
public class HpPowerModeFeatureTests
{
    private const uint CmdFanCount = 0x10;
    private const uint CmdSetPerformanceMode = 0x1A;
    private const uint CmdSystemDesignData = 0x28;

    private sealed class FakeHpBios : IHpWmiBios
    {
        private readonly Dictionary<uint, (int Rc, byte[] Data)> _responses = new();
        public bool Available { get; set; } = true;
        public List<(uint CmdType, byte[] Input)> Calls { get; } = [];

        public bool IsAvailable => Available;

        public void Seed(uint cmdType, int rc, params byte[] data) => _responses[cmdType] = (rc, data);

        public (int ReturnCode, byte[] Data) Execute(uint commandType, byte[] input)
        {
            Calls.Add((commandType, input));
            return _responses.TryGetValue(commandType, out var response)
                ? response
                : (-1, []);
        }
    }

    private static FakeHpBios SupportedBios(bool v1 = true)
    {
        var bios = new FakeHpBios();
        bios.Seed(CmdFanCount, 0, 0x02);
        bios.Seed(CmdSystemDesignData, 0, 0, 0, 0, v1 ? (byte)1 : (byte)0);
        bios.Seed(CmdSetPerformanceMode, 0);
        return bios;
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenBiosUnavailable()
    {
        SetMachineInformation(HpMachine());
        var feature = new HpPowerModeFeature(new FakeHpBios { Available = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonHpMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new HpPowerModeFeature(SupportedBios());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenProbeReturnsError()
    {
        SetMachineInformation(HpMachine());
        var bios = new FakeHpBios();
        bios.Seed(CmdFanCount, 3); // unknown command

        var feature = new HpPowerModeFeature(bios);

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0x50)]
    [InlineData(PowerModeState.Balance, 0x30)]
    [InlineData(PowerModeState.Performance, 0x31)]
    public async Task SetState_ShouldWriteV1Values(PowerModeState state, byte expectedMode)
    {
        SetMachineInformation(HpMachine());
        var bios = SupportedBios(v1: true);

        var feature = new HpPowerModeFeature(bios);
        await feature.SetStateAsync(state);

        bios.Calls.Should().Contain(c =>
            c.CmdType == CmdSetPerformanceMode &&
            c.Input.Length == 4 &&
            c.Input[0] == 0xFF &&
            c.Input[1] == expectedMode);
        (await feature.GetStateAsync()).Should().Be(state);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0x02)]
    [InlineData(PowerModeState.Balance, 0x00)]
    [InlineData(PowerModeState.Performance, 0x01)]
    public async Task SetState_ShouldWriteV0Values(PowerModeState state, byte expectedMode)
    {
        SetMachineInformation(HpMachine());
        var bios = SupportedBios(v1: false);

        var feature = new HpPowerModeFeature(bios);
        await feature.SetStateAsync(state);

        bios.Calls.Should().Contain(c =>
            c.CmdType == CmdSetPerformanceMode &&
            c.Input[1] == expectedMode);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldThrow_WhenBiosReturnsError()
    {
        SetMachineInformation(HpMachine());
        var bios = new FakeHpBios();
        bios.Seed(CmdFanCount, 0, 0x02);
        bios.Seed(CmdSetPerformanceMode, 5); // invalid parameters

        var feature = new HpPowerModeFeature(bios);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Quiet));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldRejectGodModeAndExtreme()
    {
        SetMachineInformation(HpMachine());
        var feature = new HpPowerModeFeature(SupportedBios());

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.GodMode));
        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Extreme));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task GetState_ShouldDefaultToBalance_BeforeAnyWrite()
    {
        SetMachineInformation(HpMachine());
        var feature = new HpPowerModeFeature(SupportedBios());

        (await feature.GetStateAsync()).Should().Be(PowerModeState.Balance);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldUseHpBackend_WhenLenovoAndAsusUnsupported()
    {
        SetMachineInformation(HpMachine());
        var bios = SupportedBios();

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new UnavailableAtk()),
            new HpPowerModeFeature(bios),
            new RazerPowerModeFeature(new UnavailableRazerHidController()),
            UnavailableAlienware(),
            UnavailableAcer(),
            UnavailableMsi(),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        await facade.SetStateAsync(PowerModeState.Performance);
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    private static MachineInformation HpMachine() => new()
    {
        Vendor = "HP Inc.",
        MachineType = "0000",
        Model = "OMEN 16"
    };

    private sealed class UnavailableAtk : IAsusAtkDriver
    {
        public bool IsAvailable => false;
        public int DeviceGet(uint deviceId) => -1;
        public int DeviceSet(uint deviceId, int value) => -1;
    }

    private sealed class UnavailableRazerHidController : IRazerHidController
    {
        public bool Probe() => false;
        public int? GetPerformanceMode(byte zone) => null;
        public bool SetPerformanceMode(byte zone, byte mode, bool manualFan) => false;
        public int? GetFanRpm(byte zone) => null;
    }

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
