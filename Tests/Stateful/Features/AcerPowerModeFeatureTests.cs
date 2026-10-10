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
public class AcerPowerModeFeatureTests
{
    private const string GetGamingMiscSetting = "GetGamingMiscSetting";
    private const string SetGamingMiscSetting = "SetGamingMiscSetting";
    private const string GetGamingSysInfo = "GetGamingSysInfo";

    private sealed class FakeAcerWmi : IAcerWmi
    {
        public bool Available { get; set; } = true;
        public List<(string Method, uint Input)> Calls { get; } = [];
        public int CurrentProfile { get; set; } = 0x01;
        public long SysInfoMask { get; set; } = 0xFFL << 24; // nonzero supported mask
        public bool WriteSucceeds { get; set; } = true;

        public bool IsAvailable => Available;

        public (bool Ok, long Output) Execute(string methodName, uint input)
        {
            Calls.Add((methodName, input));

            if (methodName == GetGamingSysInfo)
                return (true, SysInfoMask);

            if (methodName == GetGamingMiscSetting)
                return (true, (long)CurrentProfile << 8);

            if (methodName == SetGamingMiscSetting)
            {
                if (!WriteSucceeds)
                    return (false, -1);

                CurrentProfile = (int)((input >> 8) & 0xFF);
                return (true, 0);
            }

            return (false, -1);
        }
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenWmiUnavailable()
    {
        SetMachineInformation(AcerMachine());
        var feature = new AcerPowerModeFeature(new FakeAcerWmi { Available = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonAcerMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new AcerPowerModeFeature(new FakeAcerWmi());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenNoSensorsReported()
    {
        SetMachineInformation(AcerMachine());
        var feature = new AcerPowerModeFeature(new FakeAcerWmi { SysInfoMask = 0 });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(0x00, PowerModeState.Quiet)]
    [InlineData(0x01, PowerModeState.Balance)]
    [InlineData(0x04, PowerModeState.Performance)]
    [InlineData(0x05, PowerModeState.Performance)] // turbo
    [InlineData(0x06, PowerModeState.Quiet)]      // eco
    public async Task GetState_ShouldMapProfiles(int profile, PowerModeState expected)
    {
        SetMachineInformation(AcerMachine());
        var wmi = new FakeAcerWmi { CurrentProfile = profile };

        var feature = new AcerPowerModeFeature(wmi);

        (await feature.GetStateAsync()).Should().Be(expected);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0x00)]
    [InlineData(PowerModeState.Balance, 0x01)]
    [InlineData(PowerModeState.Performance, 0x04)]
    public async Task SetState_ShouldWriteAndVerifyReadBack(PowerModeState state, int expectedProfile)
    {
        SetMachineInformation(AcerMachine());
        var wmi = new FakeAcerWmi();

        var feature = new AcerPowerModeFeature(wmi);
        await feature.SetStateAsync(state);

        wmi.Calls.Should().Contain(c =>
            c.Method == SetGamingMiscSetting &&
            c.Input == (0x0Bu | ((uint)expectedProfile << 8)));
        (await feature.GetStateAsync()).Should().Be(state);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldThrow_WhenWriteFails()
    {
        SetMachineInformation(AcerMachine());
        var wmi = new FakeAcerWmi { WriteSucceeds = false };

        var feature = new AcerPowerModeFeature(wmi);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Quiet));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldRejectGodModeAndExtreme()
    {
        SetMachineInformation(AcerMachine());
        var feature = new AcerPowerModeFeature(new FakeAcerWmi());

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.GodMode));
        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Extreme));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldUseAcerBackend_WhenEarlierBackendsUnsupported()
    {
        SetMachineInformation(AcerMachine());

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new UnavailableAtk()),
            new HpPowerModeFeature(new UnavailableHpBios()),
            new RazerPowerModeFeature(new UnavailableRazerHidController()),
            UnavailableAlienware(),
            new AcerPowerModeFeature(new FakeAcerWmi { CurrentProfile = 0x04 }),
            UnavailableMsi(),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    private static MachineInformation AcerMachine() => new()
    {
        Vendor = "Acer Incorporated",
        MachineType = "0000",
        Model = "Predator Helios Neo 16"
    };

    private static AlienwarePowerModeFeature UnavailableAlienware() => new(new UnavailableAwccWmi());

    private static MsiPowerModeFeature UnavailableMsi() => new(new UnavailableEcChannel());

    private static TongfangPowerModeFeature UnavailableTongfang() => new(new UnavailableEcChannel());

    private static ClevoPowerModeFeature UnavailableClevo() => new(new UnavailableEcChannel());

    private sealed class UnavailableEcChannel : IEcChannel
    {
        public bool IsAvailable => false;
        public bool TryRead(byte address, out byte value) { value = 0; return false; }
        public bool TryWrite(byte address, byte value) => false;
    }

    private sealed class UnavailableAwccWmi : IAlienwareWmi
    {
        public bool IsAvailable => false;
        public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0) => -1;
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
