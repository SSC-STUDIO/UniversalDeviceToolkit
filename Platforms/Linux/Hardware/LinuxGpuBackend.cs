using System.Diagnostics;
using System.Globalization;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;

namespace UniversalDeviceToolkit.Platform.Linux.Hardware;

/// <summary>
/// Linux GPU telemetry: sysfs DRM/hwmon first (AMD/Intel/NVIDIA kernel nodes),
/// then nvidia-smi / rocm-smi when those tools are actually installed.
/// </summary>
public sealed class LinuxGpuBackend : IGpuBackend
{
    private const string NvidiaSmi = "/usr/bin/nvidia-smi";
    private const string RocmSmi = "/usr/bin/rocm-smi";
    private const string DrmRoot = "/sys/class/drm";

    private readonly ILinuxFileSystem _fs;

    public LinuxGpuBackend()
        : this(PhysicalLinuxFileSystem.Instance)
    {
    }

    public LinuxGpuBackend(ILinuxFileSystem fileSystem)
    {
        _fs = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <inheritdoc />
    public bool IsAvailable => HasSysfsGpu() || File.Exists(NvidiaSmi) || File.Exists(RocmSmi);

    /// <inheritdoc />
    public string? GetGpuName()
    {
        var sysfs = ReadSysfsName();
        if (!string.IsNullOrWhiteSpace(sysfs))
            return sysfs;

        if (File.Exists(NvidiaSmi))
        {
            var name = RunCli(NvidiaSmi, "--query-gpu=name", "--format=csv,noheader");
            if (!string.IsNullOrWhiteSpace(name))
                return name.Split('\n', 2)[0].Trim();
        }

        if (File.Exists(RocmSmi))
        {
            var line = RunCli(RocmSmi, "--showproductname", "--csv")
                ?.Split('\n').Skip(1).FirstOrDefault()?.Split(',').Skip(1).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetUsagePercent()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var busy = ReadInt(Combine(card.DeviceDir, "gpu_busy_percent"));
            if (busy is not null)
                return busy;

            var hwmonBusy = ReadHwmonInt(card.DeviceDir, "gpu_busy_percent");
            if (hwmonBusy is not null)
                return hwmonBusy;
        }

        if (File.Exists(NvidiaSmi))
        {
            var parsed = ParseInt(RunCli(NvidiaSmi, "--query-gpu=utilization.gpu", "--format=csv,noheader,nounits"));
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetTemperatureCelsius()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var temp = ReadHwmonMilliC(card.DeviceDir);
            if (temp is not null)
                return temp;
        }

        if (File.Exists(NvidiaSmi))
        {
            var parsed = ParseInt(RunCli(NvidiaSmi, "--query-gpu=temperature.gpu", "--format=csv,noheader"));
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetCurrentClockMhz()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var clock = ReadInt(Combine(card.DeviceDir, "pp_dpm_sclk")) ??
                        ParsePpDpm(Combine(card.DeviceDir, "pp_dpm_sclk")) ??
                        ReadInt(Combine(card.DeviceDir, "gt_cur_freq_mhz")) ??
                        ReadInt(Combine(card.CardDir, "gt_cur_freq_mhz"));
            if (clock is not null)
                return clock;
        }

        if (File.Exists(NvidiaSmi))
        {
            var parsed = ParseInt(RunCli(NvidiaSmi, "--query-gpu=clocks.sm", "--format=csv,noheader,nounits"));
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetBoostClockMhz()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var max = ReadInt(Combine(card.DeviceDir, "pp_max_clocks")) ??
                      ReadInt(Combine(card.DeviceDir, "gt_boost_freq_mhz")) ??
                      ReadInt(Combine(card.DeviceDir, "gt_max_freq_mhz"));
            if (max is not null)
                return max;
        }

        if (File.Exists(NvidiaSmi))
        {
            var parsed = ParseInt(RunCli(NvidiaSmi, "--query-gpu=clocks.max.sm", "--format=csv,noheader,nounits"));
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetMemoryUsedMb()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var bytes = ReadLong(Combine(card.DeviceDir, "mem_info_vram_used"));
            if (bytes is not null)
                return (int)Math.Round(bytes.Value / 1024.0 / 1024.0);
        }

        if (File.Exists(NvidiaSmi))
        {
            var parsed = ParseInt(RunCli(NvidiaSmi, "--query-gpu=memory.used", "--format=csv,noheader,nounits"));
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetMemoryTotalMb()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var bytes = ReadLong(Combine(card.DeviceDir, "mem_info_vram_total"));
            if (bytes is not null)
                return (int)Math.Round(bytes.Value / 1024.0 / 1024.0);
        }

        if (File.Exists(NvidiaSmi))
        {
            var parsed = ParseInt(RunCli(NvidiaSmi, "--query-gpu=memory.total", "--format=csv,noheader,nounits"));
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    private bool HasSysfsGpu() => EnumerateGpuCards().Count > 0;

    private string? ReadSysfsName()
    {
        foreach (var card in EnumerateGpuCards())
        {
            var product = FirstPresent(
                Trim(_fs.ReadText(Combine(card.DeviceDir, "product_name"))),
                Trim(_fs.ReadText(Combine(card.DeviceDir, "label"))),
                DriverLabel(card));
            if (!string.IsNullOrWhiteSpace(product))
                return product;
        }

        return null;
    }

    private string? DriverLabel(GpuCard card)
    {
        var uevent = _fs.ReadText(Combine(card.DeviceDir, "uevent")) ?? string.Empty;
        string? driver = null;
        foreach (var line in uevent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("DRIVER=", StringComparison.OrdinalIgnoreCase))
                driver = line["DRIVER=".Length..].Trim();
        }

        var vendor = Trim(_fs.ReadText(Combine(card.DeviceDir, "vendor")));
        var vendorName = vendor switch
        {
            "0x10de" => "NVIDIA",
            "0x1002" => "AMD",
            "0x8086" => "Intel",
            _ => null
        };

        return FirstPresent(
            vendorName is not null && driver is not null ? $"{vendorName} ({driver})" : null,
            vendorName,
            driver);
    }

    private IReadOnlyList<GpuCard> EnumerateGpuCards()
    {
        var cards = new List<GpuCard>();
        foreach (var cardDir in _fs.EnumerateDirectories(DrmRoot))
        {
            var name = Path.GetFileName(cardDir.TrimEnd('/'));
            if (name is null || !name.StartsWith("card", StringComparison.Ordinal) || name.Contains('-'))
                continue;

            var deviceDir = Combine(cardDir, "device");
            var vendor = Trim(_fs.ReadText(Combine(deviceDir, "vendor")));
            if (string.IsNullOrWhiteSpace(vendor) || vendor is "0x0000" or "0xffff")
                continue;

            var driver = Trim(_fs.ReadText(Combine(deviceDir, "uevent")));
            var looksLikeDisplayBridge = driver is not null &&
                (driver.Contains("DRIVER=simple", StringComparison.OrdinalIgnoreCase) ||
                 driver.Contains("DRIVER=vkms", StringComparison.OrdinalIgnoreCase) ||
                 driver.Contains("DRIVER=vgem", StringComparison.OrdinalIgnoreCase));
            if (looksLikeDisplayBridge)
                continue;

            cards.Add(new GpuCard(cardDir, deviceDir, vendor));
        }

        return cards;
    }

    private int? ReadHwmonMilliC(string deviceDir)
    {
        var hwmonRoot = Combine(deviceDir, "hwmon");
        foreach (var hwmonDir in _fs.EnumerateDirectories(hwmonRoot))
        {
            foreach (var input in _fs.EnumerateFiles(hwmonDir, "temp*_input"))
            {
                var milli = ReadInt(input);
                if (milli is null)
                    continue;
                var celsius = (int)Math.Round(milli.Value / 1000.0);
                if (celsius is >= -50 and <= 120)
                    return celsius;
            }
        }

        return null;
    }

    private int? ReadHwmonInt(string deviceDir, string fileName)
    {
        var hwmonRoot = Combine(deviceDir, "hwmon");
        foreach (var hwmonDir in _fs.EnumerateDirectories(hwmonRoot))
        {
            var value = ReadInt(Combine(hwmonDir, fileName));
            if (value is not null)
                return value;
        }

        return null;
    }

    private int? ParsePpDpm(string path)
    {
        var text = _fs.ReadText(path);
        if (text is null)
            return null;

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.Contains('*'))
                continue;
            var digits = new string(line.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mhz) && mhz > 0)
                return mhz;
        }

        return null;
    }

    private int? ReadInt(string path)
    {
        var raw = Trim(_fs.ReadText(path));
        return raw is not null && int.TryParse(raw.Split('\n', 2)[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private long? ReadLong(string path)
    {
        var raw = Trim(_fs.ReadText(path));
        return raw is not null && long.TryParse(raw.Split('\n', 2)[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value?.Split('\n', 2)[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static string? RunCli(string executable, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static string Combine(string directory, string relative) =>
        $"{directory.TrimEnd('/')}/{relative.TrimStart('/')}";

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record GpuCard(string CardDir, string DeviceDir, string Vendor);
}
