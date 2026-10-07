using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Overclocking.Amd;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public sealed class HardwareAccessPolicyTests : IDisposable
{
    private const string DiagnosticEnvironmentVariable = "UDT_DIAGNOSTIC_MODE";
    private readonly string? _previousDiagnosticMode = Environment.GetEnvironmentVariable(DiagnosticEnvironmentVariable);

    public HardwareAccessPolicyTests() => Environment.SetEnvironmentVariable(DiagnosticEnvironmentVariable, "1");

    [Theory]
    [InlineData(null, "--no-hardware", true)]
    [InlineData("0", "--no-hardware", true)]
    [InlineData("1", "--trace", true)]
    [InlineData(null, "--trace", false)]
    [InlineData("0", "--safe-start", false)]
    [InlineData("true", "--trace", false)]
    [InlineData(null, "--no-hardware=false", false)]
    [InlineData(null, "--NO-HARDWARE", false)]
    public void Policy_OnlyDisablesHardwareForExplicitDiagnosticMarkerOrFlag(
        string? diagnosticMode,
        string argument,
        bool expectedDisabled)
    {
        HardwareAccessPolicy.ShouldDisableHardware(["UniversalDeviceToolkit.Host.exe", argument], diagnosticMode)
            .Should().Be(expectedDisabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiagnosticSensorRead_ReturnsUnavailableWithoutCallingNativeOrVendorProbes(bool detailed)
    {
        using var gpuController = CreateGpuController();
        using var sensors = new ProbeDetectingSensorsController(gpuController);

        await sensors.PrepareAsync();
        var snapshot = await sensors.GetDataAsync(detailed);

        snapshot.CPU.Should().Be(SensorData.Empty);
        snapshot.GPU.Should().Be(SensorData.Empty);
        sensors.ProbeCount.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticDiscovery_DoesNotOpenLibreHardwareMonitor(bool full)
    {
        var discovery = new HardwareDiscoveryService();
        var mode = full ? HardwareDiscoveryService.InitializationMode.Full : HardwareDiscoveryService.InitializationMode.FanOnly;

        discovery.GetHardware(mode);
        discovery.ResetSensors();
        discovery.NeedRefreshHardware("NvidiaGPU");

        discovery.IsInitialized.Should().BeFalse();
        discovery.HardwareCount.Should().Be(0);
        discovery.Mode.Should().Be(HardwareDiscoveryService.InitializationMode.None);
    }

    [Fact]
    public async Task DiagnosticSensorGroup_RefusesLazyInitializationAndSubscriptions()
    {
        using var gpuController = CreateGpuController();
        using var sensors = new SensorsGroupController(new DefaultDelayProvider(), gpuController);

        (await sensors.IsSupportedAsync()).Should().Be(LibreHardwareMonitorInitialState.Fail);
        (await sensors.EnsureFanSensorsAvailableAsync()).Should().BeFalse();
        (await sensors.EnsureHardwareAfterBackgroundAsync()).Should().Be(LibreHardwareMonitorInitialState.Fail);
        sensors.Start(this, TimeSpan.FromMilliseconds(100));
        await sensors.UpdateAsync();

        sensors.IsLibreHardwareMonitorInitialized().Should().BeFalse();
        sensors.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void DiagnosticEmbeddedController_RefusesDriverAccessForReadsAndWrites()
    {
        var channel = new PawnIoEcChannel();

        channel.IsAvailable.Should().BeFalse();
        channel.TryRead(0x00, out var value).Should().BeFalse();
        value.Should().Be(0);
        channel.TryWrite(0x00, 0x01).Should().BeFalse();
    }

    [Fact]
    public async Task DiagnosticAmdInitialization_DoesNotCreateNativeCpuDriver()
    {
        using var controller = new AmdOverclockingController();

        await controller.InitializeAsync();

        controller.IsSupported().Should().BeFalse();
        var getCpu = () => controller.GetCpu();
        getCpu.Should().Throw<InvalidOperationException>();
    }

    public void Dispose() => Environment.SetEnvironmentVariable(DiagnosticEnvironmentVariable, _previousDiagnosticMode);

    private static GPUController CreateGpuController() => new(
        new Mock<IGPUProcessManager>().Object,
        new Mock<IGPUHardwareManager>().Object,
        new DefaultDelayProvider());

    private sealed class ProbeDetectingSensorsController(GPUController gpuController) : AbstractSensorsController(gpuController)
    {
        internal int ProbeCount { get; private set; }

        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);
        protected override int GetCpuUtilization(int maxUtilization) => UnexpectedProbe();
        protected override int GetCpuCoreClock() => UnexpectedProbe();
        protected override Task<int> GetCpuCurrentTemperatureAsync() => Task.FromResult(UnexpectedProbe());
        protected override Task<int> GetGpuCurrentTemperatureAsync() => Task.FromResult(UnexpectedProbe());
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(UnexpectedProbe());
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(UnexpectedProbe());
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(UnexpectedProbe());
        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(UnexpectedProbe());

        private int UnexpectedProbe()
        {
            ProbeCount++;
            throw new InvalidOperationException("Diagnostic sensor access attempted a hardware probe.");
        }
    }
}
