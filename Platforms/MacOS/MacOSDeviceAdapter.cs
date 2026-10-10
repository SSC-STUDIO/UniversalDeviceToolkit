using System.Globalization;
using System.Text.RegularExpressions;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Platform.MacOS;

/// <summary>
/// Safe read-only macOS machine adapter. It deliberately does not expose SMC or
/// other privileged vendor writes through the generic platform surface.
/// </summary>
public sealed class MacOSDeviceAdapter : IDeviceAdapter
{
    private readonly IPlatformCommandRunner _commandRunner;
    private readonly IReadOnlyCollection<DevicePackDefinition> _packs;

    public MacOSDeviceAdapter(
        IPlatformCommandRunner? commandRunner = null,
        IReadOnlyCollection<DevicePackDefinition>? packs = null)
    {
        _commandRunner = commandRunner ?? new ProcessPlatformCommandRunner();
        _packs = packs ?? DevicePackCatalogLoader.Load();
    }

    public string PlatformId => "macos";

    public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var modelResult = _commandRunner.Run("sysctl", "-n", "hw.model");
        var architectureResult = _commandRunner.Run("uname", "-m");
        var hardwareResult = _commandRunner.Run("system_profiler", "SPHardwareDataType");
        var modelIdentifier = modelResult.Succeeded ? modelResult.StandardOutput.Trim() : string.Empty;
        var hardwareText = hardwareResult.Succeeded ? hardwareResult.StandardOutput : string.Empty;
        var modelName = ExtractValue(hardwareText, "Model Name");
        var serial = ExtractValue(hardwareText, "Serial Number");
        var chip = ExtractValue(hardwareText, "Chip");
        var processor = ExtractValue(hardwareText, "Processor Name");
        var model = JoinPresent(" ", modelName, modelIdentifier);
        if (string.IsNullOrWhiteSpace(model))
            model = FirstPresent(modelIdentifier, chip, processor);

        var identity = new DeviceIdentity(
            "macos",
            FirstPresent(architectureResult.Succeeded ? architectureResult.StandardOutput.Trim() : string.Empty, "unknown"),
            "Apple Inc.",
            model,
            FirstPresent(modelName, modelIdentifier),
            string.Empty,
            serial,
            "macos-system-profiler");
        var support = DeviceSupportMatcher.Evaluate(identity, _packs);

        var telemetry = ReadTelemetry();
        var battery = _commandRunner.Run("pmset", "-g", "batt");
        var sensors = telemetry.Readings.Concat(ReadBattery(battery)).ToArray();
        IReadOnlyList<DeviceCapability> capabilities =
        [
            Capability("hardware-identity", !string.IsNullOrWhiteSpace(identity.Model), "sysctl/system_profiler", "Hardware identity was not returned."),
            Capability("read-only-telemetry", telemetry.IsAvailable, "sysctl", "sysctl did not return readable telemetry."),
            Capability("power-diagnostics", battery.Succeeded, "pmset", "pmset did not return battery or external power state."),
            DeviceCapability.Unavailable("gpu-management", "No safe generic macOS GPU management backend is available.", "macos"),
            DeviceCapability.Unavailable("fan-control", "Fan control requires a verified model-specific SMC backend.", "macos"),
            DeviceCapability.Unavailable("keyboard-backlight", "Keyboard backlight control is not exposed by the generic macOS adapter.", "macos"),
        ];

        return Task.FromResult(new DeviceSnapshot(
            identity,
            support,
            capabilities,
            sensors,
            battery.Succeeded ? FirstPresent(ExtractPowerSource(battery.StandardOutput), "unknown") : null,
            "macos-system-profiler/sysctl/pmset"));
    }

    private TelemetryResult ReadTelemetry()
    {
        var readings = new List<SensorReading>();
        var cpuCount = _commandRunner.Run("sysctl", "-n", "hw.ncpu");
        if (cpuCount.Succeeded && double.TryParse(cpuCount.StandardOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var logicalCpuCount))
            readings.Add(new SensorReading("Logical CPUs", "System", logicalCpuCount, "count"));

        var memory = _commandRunner.Run("sysctl", "-n", "hw.memsize");
        if (memory.Succeeded && double.TryParse(memory.StandardOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var memoryBytes))
            readings.Add(new SensorReading("Memory Total", "Memory", memoryBytes / 1024d / 1024d / 1024d, "GiB"));

        return new TelemetryResult(cpuCount.Succeeded || memory.Succeeded, readings);
    }

    private static IEnumerable<SensorReading> ReadBattery(PlatformCommandResult result)
    {
        if (!result.Succeeded)
            return [];

        var match = Regex.Match(result.StandardOutput, @"(?<percent>\d{1,3})%", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups["percent"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
            ? [new SensorReading("Battery Charge", "Battery", percent, "%")]
            : [];
    }

    private static DeviceCapability Capability(string id, bool available, string source, string unavailableReason) =>
        available
            ? new DeviceCapability(id, true, false, source, "Read-only platform data is available.")
            : DeviceCapability.Unavailable(id, unavailableReason, source);

    private static string ExtractPowerSource(string output) =>
        output.Contains("AC Power", StringComparison.OrdinalIgnoreCase) ? "AC Power" :
        output.Contains("Battery Power", StringComparison.OrdinalIgnoreCase) ? "Battery Power" :
        string.Empty;

    private static string ExtractValue(string text, string key)
    {
        var match = Regex.Match(
            text,
            $"^\\s*{Regex.Escape(key)}(?:\\s*\\(.+?\\))?:\\s*(?<value>.+?)\\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string JoinPresent(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();

    private sealed record TelemetryResult(bool IsAvailable, IReadOnlyList<SensorReading> Readings);
}
