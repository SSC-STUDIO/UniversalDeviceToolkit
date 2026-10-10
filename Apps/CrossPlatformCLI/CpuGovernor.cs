using System.Text.RegularExpressions;

internal sealed record CpuGovernorStatus(
    string Source,
    string ActiveGovernor,
    CpuGovernorPolicy[] Policies,
    CpuGovernorOption[] AvailableGovernors,
    bool CanSetGovernor,
    string[] Notes)
{
    public static CpuGovernorStatus Unknown(string source, params string[] notes) =>
        new(source, string.Empty, [], [], false, notes);
}

internal sealed record CpuGovernorPolicy(
    string Id,
    string CurrentGovernor,
    string[] AvailableGovernors,
    string ScalingGovernorPath,
    string Source);

internal sealed record CpuGovernorOption(
    string Id,
    string DisplayName,
    bool IsActive);

internal sealed class CpuGovernorReader(IFileSystem fileSystem)
{
    public CpuGovernorStatus Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxCpuGovernorProvider(fileSystem).Read();

        return CpuGovernorStatus.Unknown("runtime", "No cross-platform CPU governor provider is available for this OS.");
    }
}

internal sealed class LinuxCpuGovernorProvider(IFileSystem fileSystem)
{
    private const string CpuRoot = "/sys/devices/system/cpu";
    private const string PolicyRoot = "/sys/devices/system/cpu/cpufreq";

    public CpuGovernorStatus Read()
    {
        var policies = CollectPolicyDirectories()
            .Select(ReadPolicy)
            .OfType<CpuGovernorPolicy>()
            .OrderBy(policy => policy.Id, StringComparer.Ordinal)
            .ToArray();

        if (policies.Length == 0)
        {
            return CpuGovernorStatus.Unknown(
                "linux-cpufreq",
                "No readable Linux CPU frequency governor policies were found in /sys/devices/system/cpu.");
        }

        var activeGovernor = GetActiveGovernor(policies);
        var availableGovernors = policies
            .SelectMany(policy => policy.AvailableGovernors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(governor => governor, StringComparer.OrdinalIgnoreCase)
            .Select(governor => new CpuGovernorOption(
                governor,
                ToDisplayName(governor),
                activeGovernor.Equals(governor, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var canSetGovernor = policies.Any(policy => policy.AvailableGovernors.Length > 0);

        return new CpuGovernorStatus(
            "linux-cpufreq",
            activeGovernor,
            policies,
            availableGovernors,
            canSetGovernor,
            []);
    }

    private IEnumerable<string> CollectPolicyDirectories()
    {
        var policyDirectories = fileSystem.EnumerateDirectories(PolicyRoot)
            .Where(directory => fileSystem.GetFileName(directory).StartsWith("policy", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (policyDirectories.Length > 0)
            return policyDirectories;

        return fileSystem.EnumerateDirectories(CpuRoot)
            .Where(directory => Regex.IsMatch(fileSystem.GetFileName(directory), @"^cpu\d+$", RegexOptions.IgnoreCase))
            .Select(directory => CombinePath(directory, "cpufreq"))
            .Where(fileSystem.DirectoryExists);
    }

    private CpuGovernorPolicy? ReadPolicy(string policyDirectory)
    {
        var currentGovernor = ReadValue(fileSystem.ReadAllText(CombinePath(policyDirectory, "scaling_governor")));
        var availableGovernors = SplitGovernors(fileSystem.ReadAllText(CombinePath(policyDirectory, "scaling_available_governors")));
        if (string.IsNullOrWhiteSpace(currentGovernor) && availableGovernors.Length == 0)
            return null;

        return new CpuGovernorPolicy(
            GetPolicyId(policyDirectory),
            currentGovernor,
            availableGovernors,
            CombinePath(policyDirectory, "scaling_governor"),
            "linux-cpufreq");
    }

    private string GetPolicyId(string policyDirectory)
    {
        var id = fileSystem.GetFileName(policyDirectory);
        if (!id.Equals("cpufreq", StringComparison.OrdinalIgnoreCase))
            return id;

        var trimmed = policyDirectory.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');
        return separator < 0 ? id : fileSystem.GetFileName(trimmed[..separator]);
    }

    private static string GetActiveGovernor(CpuGovernorPolicy[] policies)
    {
        var activeGovernors = policies
            .Select(policy => policy.CurrentGovernor)
            .Where(governor => !string.IsNullOrWhiteSpace(governor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return activeGovernors.Length switch
        {
            0 => string.Empty,
            1 => activeGovernors[0],
            _ => "mixed"
        };
    }

    private static string ReadValue(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;

    internal static string[] SplitGovernors(string value) =>
        Regex.Split(value.Trim(), @"\s+")
            .Select(governor => governor.Trim())
            .Where(governor => !string.IsNullOrWhiteSpace(governor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static string ToDisplayName(string governor) =>
        governor switch
        {
            "schedutil" => "Schedutil",
            "powersave" => "Power save",
            "performance" => "Performance",
            "ondemand" => "On demand",
            "conservative" => "Conservative",
            "userspace" => "User space",
            _ => string.Join(' ', governor.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(Capitalize))
        };

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{char.ToUpperInvariant(value[0])}{value[1..]}";

    private static string CombinePath(string directory, string fileName) =>
        $"{directory.TrimEnd('/')}/{fileName}";
}

internal sealed class CpuGovernorWriter(
    IFileSystem fileSystem,
    ICommandResultRunner commandRunner,
    CrossPlatformControlPlatform platform = CrossPlatformControlPlatform.Auto)
{
    public HardwareControlSetResult SetGovernor(string value)
    {
        if (ResolvePlatform() != CrossPlatformControlPlatform.Linux)
        {
            return new HardwareControlSetResult(
                false,
                "cpu-governor",
                value,
                "CPU governor control is currently implemented for Linux cpufreq only.");
        }

        var status = new LinuxCpuGovernorProvider(fileSystem).Read();
        if (status.Policies.Length == 0)
        {
            var note = status.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
            return new HardwareControlSetResult(
                false,
                "cpu-governor",
                value,
                string.IsNullOrWhiteSpace(note) ? "No writable CPU governor policy was found." : note);
        }

        var governor = NormalizeGovernor(value, status.AvailableGovernors.Select(option => option.Id));
        if (string.IsNullOrWhiteSpace(governor))
        {
            return new HardwareControlSetResult(
                false,
                "cpu-governor",
                value,
                $"Supported CPU governors are {FormatSupportedGovernors(status.AvailableGovernors)}.");
        }

        var writablePolicies = status.Policies
            .Where(policy => policy.AvailableGovernors.Contains(governor, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (writablePolicies.Length == 0)
        {
            return new HardwareControlSetResult(
                false,
                "cpu-governor",
                governor,
                $"CPU governor '{governor}' is not reported as available for any readable policy.");
        }

        foreach (var policy in writablePolicies)
        {
            var result = commandRunner.RunResult("sh", "-c", $"printf %s {ShellQuote(governor)} > {ShellQuote(policy.ScalingGovernorPath)}");
            if (!result.Succeeded)
                return new HardwareControlSetResult(false, "cpu-governor", governor, $"{policy.Id}: {result.GetSummary()}");
        }

        return new HardwareControlSetResult(
            true,
            "cpu-governor",
            governor,
            $"Set {writablePolicies.Length} CPU governor policies to {governor}.");
    }

    private CrossPlatformControlPlatform ResolvePlatform()
    {
        if (platform != CrossPlatformControlPlatform.Auto)
            return platform;

        if (OperatingSystem.IsLinux())
            return CrossPlatformControlPlatform.Linux;

        if (OperatingSystem.IsMacOS())
            return CrossPlatformControlPlatform.MacOS;

        return CrossPlatformControlPlatform.Other;
    }

    private static string NormalizeGovernor(string value, IEnumerable<string> availableGovernors)
    {
        var normalized = NormalizeKey(value);
        var available = availableGovernors
            .Where(governor => !string.IsNullOrWhiteSpace(governor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exact = available.FirstOrDefault(governor =>
            NormalizeKey(governor).Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            NormalizeKey(governor).Replace("-", string.Empty, StringComparison.Ordinal).Equals(normalized.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        return normalized switch
        {
            "performance" or "perf" or "turbo" => FirstAvailable(available, "performance"),
            "powersave" or "power-save" or "power-saver" or "quiet" or "battery" => FirstAvailable(available, "powersave"),
            "balanced" or "balance" => FirstAvailable(available, "schedutil", "ondemand", "conservative"),
            _ => string.Empty
        };
    }

    private static string FirstAvailable(IEnumerable<string> available, params string[] preferred) =>
        preferred.FirstOrDefault(candidate => available.Contains(candidate, StringComparer.OrdinalIgnoreCase)) ?? string.Empty;

    private static string FormatSupportedGovernors(CpuGovernorOption[] options) =>
        options.Length == 0 ? "not reported by Linux cpufreq" : string.Join(", ", options.Select(option => option.Id));

    private static string NormalizeKey(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"[\s_]+", "-");

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}
