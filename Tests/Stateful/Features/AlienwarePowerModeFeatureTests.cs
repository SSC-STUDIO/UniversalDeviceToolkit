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
public class AlienwarePowerModeFeatureTests
{
    private const string ThermalInformation = "Thermal_Information";
    private const string ThermalControl = "Thermal_Control";

    private sealed class FakeAwccWmi : IAlienwareWmi
    {
        public bool Available { get; set; } = true;
        public List<(string Method, byte Op, byte Arg1)> Calls { get; } = [];
        public int CurrentProfile { get; set; } = 0xA0;
        public int Description { get; set; } = -1;
        public bool WriteSucceeds { get; set; } = true;

        public bool IsAvailable => Available;

        public void SeedDescription(int value) => Description = value;

        public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0)
        {
            Calls.Add((methodName, operation, arg1));

            if (methodName == ThermalInformation && operation == 0x0B)
                return CurrentProfile;

            if (methodName == ThermalInformation && operation == 0x02)
                return Description;

            if (methodName == ThermalControl && operation == 0x01)
            {
                if (!WriteSucceeds)
                    return -1;

                CurrentProfile = arg1;
                return 0;
            }

            return -1;
        }
    }

    private static FakeAwccWmi SupportedWmi()
    {
        var wmi = new FakeAwccWmi();
        wmi.SeedDescription(0x02 | (0x01 << 8) | (0x00 << 16) | (0x04 << 24)); // 2 fans, 1 temp, 4 profiles
        return wmi;
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenWmiUnavailable()
    {
        SetMachineInformation(DellMachine());
        var feature = new AlienwarePowerModeFeature(new FakeAwccWmi { Available = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonDellMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new AlienwarePowerModeFeature(SupportedWmi());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnMalformedSystemDescription()
    {
        SetMachineInformation(DellMachine());
        var wmi = new FakeAwccWmi();
        wmi.SeedDescription(0xFF); // no profiles

        var feature = new AlienwarePowerModeFeature(wmi);

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(0x96, PowerModeState.Quiet)]
    [InlineData(0xA3, PowerModeState.Quiet)]
    [InlineData(0x97, PowerModeState.Balance)]
    [InlineData(0xA0, PowerModeState.Balance)]
    [InlineData(0x99, PowerModeState.Performance)]
    [InlineData(0xA4, PowerModeState.Performance)]
    [InlineData(0xAB, PowerModeState.Performance)]
    [InlineData(0xA1, PowerModeState.Performance)]
    public async Task GetState_ShouldMapProfileIds(int profile, PowerModeState expected)
    {
        SetMachineInformation(DellMachine());
        var wmi = SupportedWmi();
        wmi.CurrentProfile = profile;

        var feature = new AlienwarePowerModeFeature(wmi);

        (await feature.GetStateAsync()).Should().Be(expected);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0xA3)]
    [InlineData(PowerModeState.Balance, 0xA0)]
    [InlineData(PowerModeState.Performance, 0xA4)]
    public async Task SetState_ShouldWriteUsttAndVerifyReadBack(PowerModeState state, byte expectedProfile)
    {
        SetMachineInformation(DellMachine());
        var wmi = SupportedWmi();

        var feature = new AlienwarePowerModeFeature(wmi);
        await feature.SetStateAsync(state);

        wmi.Calls.Should().Contain(c =>
            c.Method == ThermalControl && c.Op == 0x01 && c.Arg1 == expectedProfile);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldFallBackToLegacyTable()
    {
        SetMachineInformation(DellMachine());
        var wmi = SupportedWmi();
        var feature = new AlienwarePowerModeFeature(wmi);

        // USTT write fails (return -1 only for 0xA3), legacy succeeds.
        wmi.SeedDescription(0x02 | (0x01 << 8) | (0x00 << 16) | (0x04 << 24));
        var wrapped = new LegacyFallbackWmi(wmi);
        var legacyFeature = new AlienwarePowerModeFeature(wrapped);

        await legacyFeature.SetStateAsync(PowerModeState.Quiet);

        wrapped.Calls.Should().Contain(c => c.Arg1 == 0x96);
        ResetCompatibilityCache();
    }

    private sealed class LegacyFallbackWmi(IAlienwareWmi inner) : IAlienwareWmi
    {
        public List<(string Method, byte Op, byte Arg1)> Calls { get; } = [];
        public bool IsAvailable => inner.IsAvailable;

        public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0)
        {
            Calls.Add((methodName, operation, arg1));
            if (methodName == "Thermal_Control" && operation == 0x01 && arg1 == 0xA3)
                return -1; // USTT quiet unsupported on this model

            var result = inner.Execute(methodName, operation, arg1, arg2, arg3);
            return result;
        }
    }

    [Fact]
    public async Task SetState_ShouldThrow_WhenBothTablesFail()
    {
        SetMachineInformation(DellMachine());
        var wmi = SupportedWmi();
        wmi.WriteSucceeds = false;

        var feature = new AlienwarePowerModeFeature(wmi);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Quiet));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldRejectGodModeAndExtreme()
    {
        SetMachineInformation(DellMachine());
        var feature = new AlienwarePowerModeFeature(SupportedWmi());

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.GodMode));
        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Extreme));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldUseAlienwareBackend_WhenEarlierBackendsUnsupported()
    {
        SetMachineInformation(DellMachine());

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new UnavailableAtk()),
            new HpPowerModeFeature(new UnavailableHpBios()),
            new RazerPowerModeFeature(new UnavailableRazerHidController()),
            new AlienwarePowerModeFeature(SupportedWmi()),
            UnavailableAcer(),
            UnavailableMsi(),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        await facade.SetStateAsync(PowerModeState.Performance);
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    private static MachineInformation DellMachine() => new()
    {
        Vendor = "Dell Inc.",
        MachineType = "0000",
        Model = "Alienware m18"
    };

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
