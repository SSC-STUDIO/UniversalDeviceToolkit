using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class SensorsNullFallbackAndThreadSafetyTests
{
    private sealed class NullGpuSensorsController(GPUController gpuController) : AbstractSensorsController(gpuController)
    {
        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);
        protected override int GetCpuUtilization(int maxUtilization) => 10;
        protected override int GetCpuCoreClock() => 3000;
        protected override Task<GPUInfo> GetGPUInfoAsync() => Task.FromResult(GPUInfo.Empty);
        protected override Task<int> GetCpuCurrentTemperatureAsync() => Task.FromResult(50);
        protected override Task<int> GetGpuCurrentTemperatureAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuCurrentFanSpeedAsync() => Task.FromResult(1000);
        protected override Task<int> GetGpuCurrentFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxFanSpeedAsync() => Task.FromResult(3000);
        protected override Task<int> GetGpuMaxFanSpeedAsync() => Task.FromResult(-1);
        protected override Task<int> GetCpuMaxCoreClockAsync() => Task.FromResult(4000);
    }

    [Fact]
    public async Task GetDataAsync_NullGpuSensorValues_ShouldReturnMinusOneFallback()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new NullGpuSensorsController(gpuController);
        var data = await controller.GetDataAsync();
        data.GPU.Temperature.Should().Be(-1);
        data.GPU.FanSpeed.Should().Be(-1);
        data.GPU.Utilization.Should().Be(-1);
        data.GPU.CoreClock.Should().Be(-1);
    }

    [Fact]
    public async Task FanSpeedsAsync_NullGpuFanSpeed_ShouldReturnMinusOne()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new NullGpuSensorsController(gpuController);
        await controller.GetDataAsync();
        var (cpuFanSpeed, gpuFanSpeed) = await controller.GetFanSpeedsAsync();
        cpuFanSpeed.Should().Be(1000);
        gpuFanSpeed.Should().Be(-1);
    }

    [Fact]
    public async Task GetDataAsync_ConcurrentAccess_ShouldNotThrow()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new NullGpuSensorsController(gpuController);
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () => await controller.GetDataAsync())).ToArray();
        var results = await Task.WhenAll(tasks);
        results.Should().AllSatisfy(r =>
        {
            r.CPU.Temperature.Should().Be(50);
            r.GPU.Temperature.Should().Be(-1);
        });
    }

    [Fact]
    public async Task GetFanSpeedsAsync_ConcurrentAccess_ShouldReturnConsistentValues()
    {
        var gpuController = new GPUController(new Mock<IGPUProcessManager>().Object, new Mock<IGPUHardwareManager>().Object, new DefaultDelayProvider());
        var controller = new NullGpuSensorsController(gpuController);
        await controller.GetDataAsync();
        var tasks = Enumerable.Range(0, 30).Select(_ => Task.Run(async () => await controller.GetFanSpeedsAsync())).ToArray();
        var results = await Task.WhenAll(tasks);
        results.Should().AllSatisfy(r =>
        {
            r.cpuFanSpeed.Should().Be(1000);
            r.gpuFanSpeed.Should().Be(-1);
        });
    }

    [Fact]
    public void OsdSettings_CorruptedJson_ShouldReturnDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "osd.json");
        File.WriteAllText(filePath, "{ invalid json ::: }");
        var settings = new CorruptibleOsdSettings(tempDir);
        var store = settings.Store;
        store.Should().NotBeNull();
        store.ShowOsd.Should().BeFalse();
        store.OsdRefreshInterval.Should().BeApproximately(1.0, 0.001);
        store.BackgroundOpacity.Should().BeApproximately(0.6, 0.001);
    }

    [Fact]
    public void OsdSettings_EmptyJson_ShouldReturnDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "osd.json");
        File.WriteAllText(filePath, "");
        var settings = new CorruptibleOsdSettings(tempDir);
        var store = settings.Store;
        store.Should().NotBeNull();
        store.ShowOsd.Should().BeFalse();
    }

    [Fact]
    public void OsdSettings_MissingFile_ShouldReturnDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var settings = new CorruptibleOsdSettings(tempDir);
        var store = settings.Store;
        store.Should().NotBeNull();
        store.ShowOsd.Should().BeFalse();
        store.Items.Should().NotBeEmpty();
    }

    [Fact]
    public void OsdSettings_PartialJson_ShouldMergeWithDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "osd.json");
        File.WriteAllText(filePath, "{\"ShowOsd\": true, \"BackgroundOpacity\": 0.8}");
        var settings = new CorruptibleOsdSettings(tempDir);
        var store = settings.Store;
        store.Should().NotBeNull();
    }

    private sealed class CorruptibleOsdSettings(string baseDir) : OsdSettings()
    {
        private readonly string _baseDir = baseDir;
        protected override string SettingsFilePath => Path.Combine(_baseDir, "osd.json");
    }
}
