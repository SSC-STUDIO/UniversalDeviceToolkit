using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

public class AbstractSensorsControllerTests
{
    private sealed class MockSensorsController(GPUController gpuController) : AbstractSensorsController(gpuController)
    {
        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);
        protected override int GetCpuUtilization(int maxUtilization) => 12;
        protected override int GetCpuCoreClock() => 3200;
        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(new GPUInfo(25, 1450, 2200, 7000, 9000, 60, 87, -1, 0));
        protected override Task<int> GetCpuCurrentTemperatureAsync() => Task.FromResult(50);
        protected override Task<int> GetGpuCurrentTemperatureAsync() => Task.FromResult(60);
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(1000);
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(1500);
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(3000);
        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(4000);
        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(4000);
    }

    private sealed class PartialFailureSensorsController(GPUController gpuController) : AbstractSensorsController(gpuController)
    {
        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);
        protected override int GetCpuUtilization(int maxUtilization) => 18;
        protected override int GetCpuCoreClock() => 3300;
        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(GPUInfo.Empty);
        protected override Task<int> GetCpuCurrentTemperatureAsync() => Task.FromResult(55);
        protected override Task<int> GetGpuCurrentTemperatureAsync() => throw new InvalidOperationException("GPU temperature probe failed");
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(2100);
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => throw new InvalidOperationException("GPU fan probe failed");
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(5200);
        protected override Task<int> GetGpuMaxFanSpeedAsync() => throw new InvalidOperationException("GPU max fan probe failed");
        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(4200);
    }

    private sealed class DetailedCacheBypassSensorsController(GPUController gpuController) : AbstractSensorsController(gpuController)
    {
        private int _temperatureReads;

        // The detailed snapshot path performs real WMI/LibreHardwareMonitor reads that
        // are not overridden by this mock; under load they can exceed the production 2s
        // guard. Relax the outer cap so the cache-bypass contract is deterministic.
        protected override int SensorReadTimeoutSeconds => 60;

        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);
        protected override int GetCpuUtilization(int maxUtilization) => 20;
        protected override int GetCpuCoreClock() => 3000;
        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(GPUInfo.Empty);
        protected override Task<int> GetCpuCurrentTemperatureAsync() => Task.FromResult(Interlocked.Increment(ref _temperatureReads) == 1 ? 0 : 61);
        protected override Task<int> GetGpuCurrentTemperatureAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(1000);
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(3000);
        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(4000);
    }

    [Fact]
    public async Task GetDataAsync_ShouldReturnCachedData_WhenCacheIsValid()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new MockSensorsController(gpuController);

        var data1 = await controller.GetDataAsync();
        var data2 = await controller.GetDataAsync();

        data1.Should().Be(data2);
        data1.CPU.Utilization.Should().Be(12);
        data1.CPU.CoreClock.Should().Be(3200);
        data1.CPU.Temperature.Should().Be(50);
        data1.GPU.Utilization.Should().Be(25);
        data1.GPU.CoreClock.Should().Be(1450);
        data1.GPU.Temperature.Should().Be(60);
    }

    [Fact]
    public async Task GetDataAsync_ShouldUpdateCache_WhenCacheExpires()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new MockSensorsController(gpuController);

        var data1 = await controller.GetDataAsync();
        await Task.Delay(150);
        var data2 = await controller.GetDataAsync();

        data1.CPU.Temperature.Should().Be(50);
        data1.GPU.Temperature.Should().Be(60);
        data2.CPU.Temperature.Should().Be(50);
        data2.GPU.Temperature.Should().Be(60);
    }

    [Fact]
    public async Task CacheAccess_ShouldBeThreadSafe()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new MockSensorsController(gpuController);

        var tasks = new List<Task<SensorsData>>();
        for (var i = 0; i < 10; i++)
            tasks.Add(controller.GetDataAsync());

        var results = await Task.WhenAll(tasks);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.CPU.Temperature == 50 && r.GPU.Temperature == 60);
    }

    [Fact]
    public async Task FanSpeedsAsync_ShouldReturnFromCache_WhenCacheIsValid()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new MockSensorsController(gpuController);

        await controller.GetDataAsync();
        var fanSpeeds = await controller.GetFanSpeedsAsync();

        fanSpeeds.cpuFanSpeed.Should().Be(1000);
        fanSpeeds.gpuFanSpeed.Should().Be(1500);
    }

    [Fact]
    public async Task GetDataAsync_WhenGpuReadFails_ShouldStillReturnCpuData()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new PartialFailureSensorsController(gpuController);

        var data = await controller.GetDataAsync();

        data.CPU.Utilization.Should().Be(18);
        data.CPU.CoreClock.Should().Be(3300);
        data.CPU.Temperature.Should().Be(55);
        data.CPU.FanSpeed.Should().Be(2100);
        data.GPU.Temperature.Should().Be(-1);
        data.GPU.FanSpeed.Should().Be(-1);
    }

    private sealed class SlowSensorsController(GPUController gpuController) : AbstractSensorsController(gpuController)
    {
        protected override int SensorReadTimeoutSeconds => 1;

        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);
        protected override int GetCpuUtilization(int maxUtilization) => 10;
        protected override int GetCpuCoreClock() => 3000;
        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(GPUInfo.Empty);
        protected override async Task<int> GetCpuCurrentTemperatureAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            return 50;
        }
        protected override Task<int> GetGpuCurrentTemperatureAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(1000);
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(3000);
        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(4000);
    }

    [Fact]
    public async Task GetDataAsync_WhenReadExceedsTimeout_ShouldReturnWithoutWaitingForSlowProbes()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new SlowSensorsController(gpuController);

        var started = DateTime.UtcNow;
        var data = await controller.GetDataAsync();
        var elapsed = DateTime.UtcNow - started;

        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
        data.CPU.Temperature.Should().Be(-1);
        data.GPU.Temperature.Should().Be(-1);
    }

    [Fact]
    public async Task GetDataAsync_DetailedCall_ShouldBypassRecentSummaryCache()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new DetailedCacheBypassSensorsController(gpuController);

        var summary = await controller.GetDataAsync(false);
        var detailed = await controller.GetDataAsync(true);

        summary.CPU.Temperature.Should().Be(-1);
        detailed.CPU.Temperature.Should().Be(61);
    }
}
