using UniversalDeviceToolkit.Platform.Linux.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class LinuxSensorBackendTests
{
    [Fact]
    public void GetReadings_ShouldProjectProcSysfsIntoCanonicalCpuMemoryBattery()
    {
        var fs = new MemoryLinuxFileSystem(new Dictionary<string, string>
        {
            ["/proc/stat"] = "cpu  100 0 50 850 0 0 0 0 0 0\ncpu0 100 0 50 850 0 0 0 0 0 0\n",
            ["/proc/meminfo"] = "MemTotal:        16384000 kB\nMemAvailable:     8192000 kB\n",
            ["/proc/cpuinfo"] = "processor\t: 0\nmodel name\t: AMD Ryzen 7 5800H\ncpu MHz\t\t: 3200.000\n",
            ["/sys/class/hwmon/hwmon0/name"] = "k10temp\n",
            ["/sys/class/hwmon/hwmon0/temp1_input"] = "45123\n",
            ["/sys/class/hwmon/hwmon0/temp1_label"] = "Tctl\n",
            ["/sys/class/hwmon/hwmon1/name"] = "amdgpu\n",
            ["/sys/class/hwmon/hwmon1/temp1_input"] = "62000\n",
            ["/sys/class/hwmon/hwmon1/temp1_label"] = "edge\n",
            ["/sys/class/thermal/thermal_zone0/type"] = "x86_pkg_temp\n",
            ["/sys/class/thermal/thermal_zone0/temp"] = "44000\n",
            ["/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"] = "3200000\n",
            ["/sys/class/power_supply/BAT0/type"] = "Battery\n",
            ["/sys/class/power_supply/BAT0/present"] = "1\n",
            ["/sys/class/power_supply/BAT0/capacity"] = "87\n",
            ["/sys/class/power_supply/BAT0/status"] = "Discharging\n",
            ["/sys/class/power_supply/BAT0/voltage_now"] = "12000000\n",
            ["/sys/class/power_supply/BAT0/power_now"] = "15000000\n",
            ["/sys/class/power_supply/AC/type"] = "Mains\n",
            ["/sys/class/power_supply/AC/online"] = "1\n",
        });

        var backend = new LinuxSensorBackend(fs);
        Assert.True(backend.IsAvailable);

        fs.Set("/proc/stat", "cpu  140 0 70 890 0 0 0 0 0 0\ncpu0 140 0 70 890 0 0 0 0 0 0\n");
        var readings = backend.GetReadings();

        var cpuUsage = Assert.Single(readings, reading => reading.Name == "CPU" && reading.Category == "Usage");
        Assert.Equal(60.0, cpuUsage.Value, 1);
        Assert.Equal("%", cpuUsage.Unit);

        var cpuTemp = Assert.Single(readings, reading => reading.Name == "CPU" && reading.Category == "Temperature");
        Assert.Equal(45.1, cpuTemp.Value, 1);

        var gpuTemp = Assert.Single(readings, reading => reading.Name == "GPU" && reading.Category == "Temperature");
        Assert.Equal(62.0, gpuTemp.Value, 1);

        var memoryUsed = Assert.Single(readings, reading => reading.Name == "Memory" && reading.Category == "Used");
        Assert.Equal(8000.0, memoryUsed.Value, 1);

        var memoryUsage = Assert.Single(readings, reading => reading.Name == "Memory" && reading.Category == "Usage");
        Assert.Equal(50.0, memoryUsage.Value, 1);

        var battery = Assert.Single(readings, reading => reading.Name == "Battery" && reading.Category == "Charge");
        Assert.Equal(87.0, battery.Value, 1);

        Assert.Contains(readings, reading => reading.Category == "Identity" && reading.Name.Contains("Ryzen", StringComparison.Ordinal));
        Assert.Contains(readings, reading => reading.Name == "CPU" && reading.Category == "Frequency" && reading.Value == 3200.0);
        Assert.Contains(readings, reading => reading.Name == "x86 pkg temp" && reading.Category == "Temperature");
    }

    [Fact]
    public void GetReadings_ShouldUseThermalZoneWhenHwmonHasNoCpuTemp()
    {
        var fs = new MemoryLinuxFileSystem(new Dictionary<string, string>
        {
            ["/proc/stat"] = "cpu  1 0 1 98 0 0 0 0\n",
            ["/sys/class/thermal/thermal_zone0/type"] = "x86_pkg_temp\n",
            ["/sys/class/thermal/thermal_zone0/temp"] = "51200\n",
        });

        var readings = new LinuxSensorBackend(fs).GetReadings();
        var cpuTemp = Assert.Single(readings, reading => reading.Name == "CPU" && reading.Category == "Temperature");
        Assert.Equal(51.2, cpuTemp.Value, 1);
    }

    [Fact]
    public void PhysicalBackend_ShouldReadLiveLinuxProcOnThisHost()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var backend = new LinuxSensorBackend();
        Assert.True(backend.IsAvailable);

        var readings = backend.GetReadings();
        Assert.Contains(readings, reading =>
            reading.Name == "Memory" &&
            reading.Category == "Total" &&
            reading.Value > 0);

        Assert.Contains(readings, reading =>
            reading.Category == "Identity" &&
            !string.IsNullOrWhiteSpace(reading.Name));
    }
}
