using UniversalDeviceToolkit.Platform.Linux.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class LinuxGpuBackendTests
{
    [Fact]
    public void SysfsDrm_ShouldExposeNameUsageTemperatureAndVram()
    {
        var fs = new MemoryLinuxFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/drm/card0/device/vendor"] = "0x1002\n",
            ["/sys/class/drm/card0/device/uevent"] = "DRIVER=amdgpu\n",
            ["/sys/class/drm/card0/device/product_name"] = "AMD Radeon RX 6800M\n",
            ["/sys/class/drm/card0/device/gpu_busy_percent"] = "23\n",
            ["/sys/class/drm/card0/device/mem_info_vram_used"] = "1073741824\n",
            ["/sys/class/drm/card0/device/mem_info_vram_total"] = "8589934592\n",
            ["/sys/class/drm/card0/device/hwmon/hwmon2/temp1_input"] = "67000\n",
        });

        var gpu = new LinuxGpuBackend(fs);

        Assert.True(gpu.IsAvailable);
        Assert.Equal("AMD Radeon RX 6800M", gpu.GetGpuName());
        Assert.Equal(23, gpu.GetUsagePercent());
        Assert.Equal(67, gpu.GetTemperatureCelsius());
        Assert.Equal(1024, gpu.GetMemoryUsedMb());
        Assert.Equal(8192, gpu.GetMemoryTotalMb());
    }
}
