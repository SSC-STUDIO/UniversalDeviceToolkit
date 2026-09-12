using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GenericSensorsControllerTests : UnitTestBase
{
    private class FallbackSensorsController : AbstractSensorsController
    {
        private readonly int _cpuUtilization;
        private readonly int _cpuTemperature;
        private readonly int _cpuCoreClock;
        private readonly int _cpuWattage;
        private readonly double _cpuVoltage;
        private readonly int _gpuUtilization;
        private readonly int _gpuTemperature;
        private readonly int _gpuCoreClock;
        private readonly int _gpuMemoryClock;
        private readonly int _gpuWattage;
        private readonly double _gpuVoltage;

        internal FallbackSensorsController(
            GPUController gpuController,
            int cpuUtilization,
            int cpuTemperature,
            int cpuCoreClock,
            int gpuUtilization,
            int gpuTemperature,
            int gpuCoreClock,
            int gpuMemoryClock = -1,
            int cpuWattage = 0,
            double cpuVoltage = 0,
            int gpuWattage = 0,
            double gpuVoltage = 0)
            : base(gpuController)
        {
            _cpuUtilization = cpuUtilization;
            _cpuTemperature = cpuTemperature;
            _cpuCoreClock = cpuCoreClock;
            _cpuWattage = cpuWattage;
            _cpuVoltage = cpuVoltage;
            _gpuUtilization = gpuUtilization;
            _gpuTemperature = gpuTemperature;
            _gpuCoreClock = gpuCoreClock;
            _gpuMemoryClock = gpuMemoryClock;
            _gpuWattage = gpuWattage;
            _gpuVoltage = gpuVoltage;
        }

        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);

        protected override Task<int> GetCpuCurrentTemperatureAsync() => Task.FromResult(-1);
        protected override Task<int> GetGpuCurrentTemperatureAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(5000);
        protected override int GetCpuUtilization(int maxUtilization) => -1;
        protected override int GetCpuCoreClock() => -1;
        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(GPUInfo.Empty);
        protected override Task<LibreHardwareMonitorReadings?> GetLibreHardwareMonitorReadingsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LibreHardwareMonitorReadings?>(new LibreHardwareMonitorReadings(
                _cpuUtilization,
                _cpuTemperature,
                _cpuCoreClock,
                _cpuWattage,
                _cpuVoltage,
                -1,
                _gpuUtilization,
                _gpuTemperature,
                _gpuCoreClock,
                _gpuMemoryClock,
                _gpuWattage,
                _gpuVoltage,
                -1));
    }

    private sealed class CpuWattageFallbackSensorsController(
        GPUController gpuController,
        int performanceCounterWattage,
        int wmiWattage,
        int libreHardwareMonitorWattage)
        : FallbackSensorsController(
            gpuController,
            cpuUtilization: -1,
            cpuTemperature: -1,
            cpuCoreClock: -1,
            gpuUtilization: -1,
            gpuTemperature: -1,
            gpuCoreClock: -1)
    {
        protected override int GetCpuWattageFromPerformanceCounter() => performanceCounterWattage;

        protected override Task<int> GetCpuWattageFromWmiAsync() => Task.FromResult(wmiWattage);

        protected override Task<int> GetCpuWattageFromLibreHardwareMonitorAsync() =>
            Task.FromResult(libreHardwareMonitorWattage);
    }

    private sealed class TestableGenericSensorsController(
        GPUController gpuController,
        Func<Task<bool>> canReadSnapshot,
        IDelayProvider? delayProvider = null,
        int cpuFanSpeed = -1,
        int gpuFanSpeed = -1)
        : GenericSensorsController(gpuController, delayProvider)
    {
        protected override Task<bool> CanReadGenericSnapshotAsyncCore() => canReadSnapshot();

        protected override Task<int> ReadLenovoCpuFanSpeedAsync() => Task.FromResult(cpuFanSpeed);

        protected override Task<int> ReadLenovoGpuFanSpeedAsync() => Task.FromResult(gpuFanSpeed);

        internal async Task<(int cpuFanSpeed, int gpuFanSpeed)> ReadLenovoFanSpeedsAsync() =>
            (await GetCpuCurrentFanSpeedAsync(), await GetGpuCurrentFanSpeedAsync());
    }

    [Fact]
    public void GenericSensorsController_ShouldBeAssignableToSensorsController()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new GenericSensorsController(gpuController);

        controller.Should().BeAssignableTo<ISensorsController>();
    }

    [Fact]
    public async Task GenericSensorsController_ShouldPreserveLenovoFanReadingsWhenGenericFallbackIsSelected()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        using var controller = new TestableGenericSensorsController(
            gpuController,
            () => Task.FromResult(true),
            cpuFanSpeed: 2380,
            gpuFanSpeed: 2510);

        var fanSpeeds = await controller.ReadLenovoFanSpeedsAsync();

        fanSpeeds.cpuFanSpeed.Should().Be(2380);
        fanSpeeds.gpuFanSpeed.Should().Be(2510);
    }

    [Fact]
    public async Task GenericSensorsController_GetDataAsync_ShouldNotThrowWhenVendorSensorsAreUnavailable()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        using var controller = new GenericSensorsController(gpuController);

        var act = () => controller.GetDataAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GenericSensorsController_IsSupportedAsync_ShouldAllowFallbackOnSupportedMachines()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        using var controller = new TestableGenericSensorsController(gpuController, () => Task.FromResult(true));

        var supported = await controller.IsSupportedAsync();

        supported.Should().BeTrue();
    }

    [Fact]
    public async Task GenericSensorsController_IsSupportedAsync_ShouldRetryWhenFirstSnapshotHasNoData()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var delayProvider = new Mock<IDelayProvider>();
        delayProvider
            .Setup(provider => provider.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var attempts = 0;
        using var controller = new TestableGenericSensorsController(
            gpuController,
            () => Task.FromResult(Interlocked.Increment(ref attempts) > 1),
            delayProvider.Object);

        var supported = await controller.IsSupportedAsync();

        supported.Should().BeTrue();
        attempts.Should().Be(2);
        delayProvider.Verify(provider => provider.Delay(TimeSpan.FromMilliseconds(250), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AbstractSensorsController_GetDataAsync_ShouldUseLibreHardwareMonitorFallbackForSummaryMetrics()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        using var controller = new FallbackSensorsController(
            gpuController,
            cpuUtilization: 37,
            cpuTemperature: 71,
            cpuCoreClock: 4123,
            gpuUtilization: 84,
            gpuTemperature: 67,
            gpuCoreClock: 1785,
            gpuMemoryClock: 9200);

        var data = await controller.GetDataAsync();

        data.CPU.Utilization.Should().Be(37);
        data.CPU.Temperature.Should().Be(71);
        data.CPU.CoreClock.Should().Be(4123);
        data.GPU.Utilization.Should().Be(84);
        data.GPU.Temperature.Should().Be(67);
        data.GPU.CoreClock.Should().Be(1785);
        data.GPU.MemoryClock.Should().Be(9200);
        data.GPU.MaxCoreClock.Should().Be(1785);
        data.GPU.MaxMemoryClock.Should().Be(9200);
    }

    [Fact]
    public async Task AbstractSensorsController_GetDataAsync_ShouldContinueCpuWattageFallbackWhenWmiReturnsZero()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        using var controller = new CpuWattageFallbackSensorsController(
            gpuController,
            performanceCounterWattage: -1,
            wmiWattage: 0,
            libreHardwareMonitorWattage: 42);

        var data = await controller.GetDataAsync(detailed: true);

        data.CPU.Wattage.Should().Be(42);
    }

    [Fact]
    public void IoCModule_ShouldResolveSensorsControllerWithGenericFallback()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<UniversalDeviceToolkit.Lib.IoCModule>();
        builder.RegisterModule<TestIoCModule>();
        builder.RegisterType<UniversalDeviceToolkit.Lib.Settings.ApplicationSettings>().AsSelf();

        using var container = builder.Build();

        var controller = container.Resolve<ISensorsController>();
        var genericController = container.Resolve<GenericSensorsController>();

        controller.Should().BeOfType<SensorsController>();
        genericController.Should().NotBeNull();
    }
}
