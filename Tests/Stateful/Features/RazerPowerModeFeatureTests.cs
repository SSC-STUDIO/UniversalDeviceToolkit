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
public class RazerPacketTests
{
    [Fact]
    public void BuildReport_ShouldLayout90BytePacketWithCrc()
    {
        var report = RazerPacket.BuildReport(0x0D, 0x02, [0x00, 0x01, 0x01, 0x00]);

        report.Length.Should().Be(91);
        report[0].Should().Be(0x00); // report id
        report[1].Should().Be(RazerPacket.StatusNew);
        report[2].Should().Be(RazerPacket.TransactionId);
        report[6].Should().Be(4);    // data size
        report[7].Should().Be(0x0D); // class
        report[8].Should().Be(0x02); // command
        report[9].Should().Be(0x00);
        report[10].Should().Be(0x01);
        report[11].Should().Be(0x01);
        report[12].Should().Be(0x00);

        var expectedCrc = (byte)0;
        for (var i = 2; i < 88; i++)
            expectedCrc ^= report[1 + i];
        report[89].Should().Be(expectedCrc);
        report[90].Should().Be(0x00); // reserved
    }

    [Fact]
    public void IsValidResponse_ShouldRequireSuccessStatusAndEcho()
    {
        var response = RazerPacket.BuildReport(0x0D, 0x82, [0x00, 0x01, 0x00, 0x00]);
        response[1] = RazerPacket.StatusSuccessful;

        RazerPacket.IsValidResponse(response, 0x0D, 0x82).Should().BeTrue();
        RazerPacket.IsValidResponse(response, 0x0D, 0x02).Should().BeFalse();

        response[1] = 0x05; // not supported
        RazerPacket.IsValidResponse(response, 0x0D, 0x82).Should().BeFalse();
    }
}

[Trait("Category", TestCategories.Unit)]
[Collection("PowerModeFeatureTests")]
public class RazerHidControllerTests
{
    private sealed class FakeHid : IRazerHid
    {
        public string[] Paths { get; set; } = [];
        public List<byte[]> Sent { get; } = [];
        public Func<byte[], byte[]?>? Responder { get; set; }

        public string[] EnumerateDevicePaths(ushort vendorId) => Paths;
        public bool GetVidPid(string devicePath, out ushort vendorId, out ushort productId)
        {
            vendorId = RazerHidController.RazerVendorId;
            productId = 0x029F;
            return true;
        }

        public bool TrySendFeatureReport(string devicePath, byte[] report)
        {
            Sent.Add(report);
            return true;
        }

        public bool TryGetFeatureReport(string devicePath, byte[] report)
        {
            var response = Responder?.Invoke(report);
            if (response is null)
                return false;

            Array.Copy(response, report, response.Length);
            return true;
        }
    }

    private static byte[]? EchoWithSuccess(byte[] report)
    {
        if (report[1] != RazerPacket.StatusNew)
            return null;

        var response = (byte[])report.Clone();
        response[1] = RazerPacket.StatusSuccessful;
        return response;
    }

    [Fact]
    public void Probe_ShouldPickFirstAnsweringCollection()
    {
        var hid = new FakeHid { Paths = ["razer-hid-0", "razer-hid-1"], Responder = EchoWithSuccess };
        var controller = new RazerHidController(hid);

        controller.Probe().Should().BeTrue();
        hid.Sent.Should().NotBeEmpty();
        RazerPacket.IsValidResponse(hid.Sent[0], 0x0D, 0x82).Should().BeFalse(); // request status is New
        hid.Sent[0][7].Should().Be(0x0D);
        hid.Sent[0][8].Should().Be(RazerPacket.CmdGetPerformanceMode);
    }

    [Fact]
    public void Probe_ShouldFail_WhenNothingAnswers()
    {
        var hid = new FakeHid { Paths = ["razer-hid-0"], Responder = _ => null };
        var controller = new RazerHidController(hid);

        controller.Probe().Should().BeFalse();
    }

    [Fact]
    public void SetPerformanceMode_ShouldSendPerZoneArguments()
    {
        var hid = new FakeHid { Paths = ["razer-hid-0"], Responder = EchoWithSuccess };
        var controller = new RazerHidController(hid);

        controller.SetPerformanceMode(RazerPacket.ZoneGpu, 0x01, manualFan: false).Should().BeTrue();

        var write = hid.Sent.Last();
        write[7].Should().Be(0x0D);
        write[8].Should().Be(RazerPacket.CmdSetPerformanceMode);
        write[6].Should().Be(4);
        write[10].Should().Be(RazerPacket.ZoneGpu);
        write[11].Should().Be(0x01);
        write[12].Should().Be(0x00);
    }

    [Fact]
    public void GetFanRpm_ShouldScaleResponseByHundred()
    {
        var hid = new FakeHid
        {
            Paths = ["razer-hid-0"],
            Responder = report =>
            {
                var response = EchoWithSuccess(report);
                response![11] = 42; // args[2] → 4200 RPM
                return response;
            },
        };
        var controller = new RazerHidController(hid);

        controller.GetFanRpm(RazerPacket.ZoneCpu).Should().Be(4200);
    }
}

[Trait("Category", TestCategories.Unit)]
[Collection("PowerModeFeatureTests")]
public class RazerPowerModeFeatureTests
{
    private sealed class FakeController : IRazerHidController
    {
        public bool ProbeResult { get; set; } = true;
        public bool CpuWriteResult { get; set; } = true;
        public int? CpuMode { get; set; } = 0x00;
        public List<(byte Zone, byte Mode, bool Manual)> Writes { get; } = [];

        public bool Probe() => ProbeResult;
        public int? GetPerformanceMode(byte zone) => zone == RazerPacket.ZoneCpu ? CpuMode : null;
        public bool SetPerformanceMode(byte zone, byte mode, bool manualFan)
        {
            if (zone == RazerPacket.ZoneGpu)
            {
                Writes.Add((zone, mode, manualFan));
                return false; // tolerated (no dGPU)
            }

            Writes.Add((zone, mode, manualFan));
            if (!CpuWriteResult)
                return false;

            CpuMode = mode;
            return true;
        }
        public int? GetFanRpm(byte zone) => null;
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonRazerMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var feature = new RazerPowerModeFeature(new FakeController());

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenProbeFails()
    {
        SetMachineInformation(RazerMachine());
        var feature = new RazerPowerModeFeature(new FakeController { ProbeResult = false });

        (await feature.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(0x00, PowerModeState.Balance)]
    [InlineData(0x01, PowerModeState.Performance)]
    [InlineData(0x02, PowerModeState.Performance)]
    [InlineData(0x04, PowerModeState.Performance)]
    [InlineData(0x05, PowerModeState.Quiet)]
    public async Task GetState_ShouldMapEcValues(byte raw, PowerModeState expected)
    {
        SetMachineInformation(RazerMachine());
        var feature = new RazerPowerModeFeature(new FakeController { CpuMode = raw });

        (await feature.GetStateAsync()).Should().Be(expected);
        ResetCompatibilityCache();
    }

    [Theory]
    [InlineData(PowerModeState.Quiet, 0x05)]
    [InlineData(PowerModeState.Balance, 0x00)]
    [InlineData(PowerModeState.Performance, 0x01)]
    public async Task SetState_ShouldWriteBothZonesWithGpuTolerated(PowerModeState state, byte expectedMode)
    {
        SetMachineInformation(RazerMachine());
        var controller = new FakeController();
        var feature = new RazerPowerModeFeature(controller);

        await feature.SetStateAsync(state);

        controller.Writes.Should().HaveCount(2);
        controller.Writes[0].Should().Be((RazerPacket.ZoneCpu, expectedMode, false));
        controller.Writes[1].Zone.Should().Be(RazerPacket.ZoneGpu);
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task SetState_ShouldThrow_WhenCpuZoneWriteFails()
    {
        SetMachineInformation(RazerMachine());
        var controller = new FakeController { ProbeResult = true, CpuWriteResult = false };
        var feature = new RazerPowerModeFeature(controller);

        await Assert.ThrowsAnyAsync<Exception>(() => feature.SetStateAsync(PowerModeState.Quiet));
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task Facade_ShouldUseRazerBackend_WhenEarlierBackendsUnsupported()
    {
        SetMachineInformation(RazerMachine());

        var facade = new PowerModeFeature(
            new TestLenovoBackend(supported: false),
            new AsusPowerModeFeature(new UnavailableAtk()),
            new HpPowerModeFeature(new UnavailableHpBios()),
            new RazerPowerModeFeature(new FakeController { CpuMode = 0x01 }),
            UnavailableAlienware(),
            UnavailableAcer(),
            UnavailableMsi(),
            UnavailableTongfang(),
            UnavailableClevo());

        (await facade.IsSupportedAsync()).Should().BeTrue();
        (await facade.GetStateAsync()).Should().Be(PowerModeState.Performance);
        ResetCompatibilityCache();
    }

    private static MachineInformation RazerMachine() => new()
    {
        Vendor = "Razer Inc.",
        MachineType = "0000",
        Model = "Razer Blade 16"
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
