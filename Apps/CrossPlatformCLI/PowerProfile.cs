using System.Text.RegularExpressions;

internal sealed record PowerProfileStatus(
    string Source,
    string ActiveProfile,
    PowerProfileOption[] AvailableProfiles,
    bool CanSetProfile,
    string[] Notes)
{
    public static PowerProfileStatus Unknown(string source, params string[] notes) =>
        new(source, string.Empty, [], false, notes);
}

internal sealed record PowerProfileOption(
    string Id,
    string DisplayName,
    bool IsActive);

internal sealed record PowerProfileChangeResult(
    bool Succeeded,
    string ProfileId,
    string Detail);

internal sealed class PowerProfileReader(
    ICommandRunner commandRunner)
{
    public PowerProfileStatus Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxPowerProfileProvider(commandRunner).Read();

        if (OperatingSystem.IsMacOS())
            return new MacPowerProfileProvider(commandRunner).Read();

        return PowerProfileStatus.Unknown("runtime", "No cross-platform power profile provider is available for this OS.");
    }
}

internal sealed class PowerProfileWriter(
    ICommandResultRunner commandRunner)
{
    public PowerProfileChangeResult SetProfile(string requestedProfile)
    {
        if (OperatingSystem.IsLinux())
            return new LinuxPowerProfileProvider(commandRunner).SetProfile(requestedProfile);

        if (OperatingSystem.IsMacOS())
            return new MacPowerProfileProvider(commandRunner).SetProfile(requestedProfile);

        return new PowerProfileChangeResult(false, requestedProfile, "No cross-platform power profile writer is available for this OS.");
    }
}

internal sealed class LinuxPowerProfileProvider(ICommandRunner commandRunner)
{
    private static readonly PowerProfileOption[] KnownProfiles =
    [
        new("power-saver", "Power saver", false),
        new("balanced", "Balanced", false),
        new("performance", "Performance", false)
    ];

    public PowerProfileStatus Read()
    {
        var output = commandRunner.Run("powerprofilesctl");
        if (string.IsNullOrWhiteSpace(output))
        {
            return PowerProfileStatus.Unknown(
                "linux-powerprofilesctl",
                "powerprofilesctl was not available or returned no data.");
        }

        var activeProfile = ExtractValue(output, @"^\s*\*\s*(?<value>[a-z0-9-]+):");
        if (string.IsNullOrWhiteSpace(activeProfile))
            activeProfile = ExtractValue(output, @"^\s*Driver:\s*.+?active:\s*(?<value>[a-z0-9-]+)\s*$");

        var available = Regex.Matches(output, @"^\s*(?:\*\s*)?(?<id>power-saver|balanced|performance):", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(match => match.Groups["id"].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new PowerProfileOption(id, ToDisplayName(id), id.Equals(activeProfile, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (available.Length == 0)
            available = KnownProfiles.Select(profile => profile with { IsActive = profile.Id.Equals(activeProfile, StringComparison.OrdinalIgnoreCase) }).ToArray();

        return new PowerProfileStatus(
            "linux-powerprofilesctl",
            activeProfile,
            available,
            true,
            []);
    }

    public PowerProfileChangeResult SetProfile(string requestedProfile)
    {
        if (commandRunner is not ICommandResultRunner resultRunner)
            return new PowerProfileChangeResult(false, requestedProfile, "Command runner does not support write result reporting.");

        var profile = NormalizeProfile(requestedProfile);
        if (string.IsNullOrWhiteSpace(profile))
        {
            return new PowerProfileChangeResult(
                false,
                requestedProfile,
                "Supported Linux power profiles are power-saver, balanced, and performance.");
        }

        var result = resultRunner.RunResult("powerprofilesctl", "set", profile);
        return result.Succeeded
            ? new PowerProfileChangeResult(true, profile, $"powerprofilesctl set {profile} completed.")
            : new PowerProfileChangeResult(false, profile, result.GetSummary());
    }

    private static string NormalizeProfile(string value)
    {
        var normalized = NormalizeKey(value);
        return normalized switch
        {
            "powersaver" or "power-saver" or "quiet" or "battery" => "power-saver",
            "balanced" or "balance" => "balanced",
            "performance" or "perf" => "performance",
            _ => string.Empty
        };
    }

    private static string ExtractValue(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static string ToDisplayName(string profile) =>
        profile.Equals("power-saver", StringComparison.OrdinalIgnoreCase)
            ? "Power saver"
            : string.Join(' ', profile.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize));

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{char.ToUpperInvariant(value[0])}{value[1..]}";

    private static string NormalizeKey(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"[\s_]+", "-");
}

internal sealed class MacPowerProfileProvider(ICommandRunner commandRunner)
{
    public PowerProfileStatus Read()
    {
        var output = commandRunner.Run("pmset", "-g", "custom");
        if (string.IsNullOrWhiteSpace(output))
        {
            return PowerProfileStatus.Unknown(
                "macos-pmset",
                "pmset returned no power profile data.");
        }

        var lowPowerMode = ExtractPmsetInt(output, "lowpowermode");
        var activeProfile = lowPowerMode switch
        {
            1 => "low-power",
            0 => "automatic",
            _ => string.Empty
        };

        return new PowerProfileStatus(
            "macos-pmset",
            activeProfile,
            [
                new("automatic", "Automatic", activeProfile.Equals("automatic", StringComparison.OrdinalIgnoreCase)),
                new("low-power", "Low power", activeProfile.Equals("low-power", StringComparison.OrdinalIgnoreCase))
            ],
            true,
            lowPowerMode is null ? ["pmset lowpowermode was not reported."] : []);
    }

    public PowerProfileChangeResult SetProfile(string requestedProfile)
    {
        if (commandRunner is not ICommandResultRunner resultRunner)
            return new PowerProfileChangeResult(false, requestedProfile, "Command runner does not support write result reporting.");

        var profile = NormalizeProfile(requestedProfile);
        if (string.IsNullOrWhiteSpace(profile))
        {
            return new PowerProfileChangeResult(
                false,
                requestedProfile,
                "Supported macOS power profiles are automatic and low-power.");
        }

        var lowPowerMode = profile.Equals("low-power", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
        var result = resultRunner.RunResult("pmset", "-a", "lowpowermode", lowPowerMode);
        return result.Succeeded
            ? new PowerProfileChangeResult(true, profile, $"pmset lowpowermode {lowPowerMode} completed.")
            : new PowerProfileChangeResult(false, profile, result.GetSummary());
    }

    private static int? ExtractPmsetInt(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"^\s*{Regex.Escape(key)}\s+(?<value>\d+)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["value"].Value, out var value) ? value : null;
    }

    private static string NormalizeProfile(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[\s_]+", "-");
        return normalized switch
        {
            "automatic" or "auto" or "normal" or "balanced" or "balance" => "automatic",
            "low-power" or "lowpower" or "power-saver" or "powersaver" or "quiet" or "battery" => "low-power",
            _ => string.Empty
        };
    }
}
