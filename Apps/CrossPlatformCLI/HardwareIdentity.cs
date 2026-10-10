using System.Diagnostics;
using System.Text.RegularExpressions;

internal sealed record HardwareIdentity(
    string Vendor,
    string Model,
    string ProductName,
    string SerialNumber,
    string Source)
{
    public static HardwareIdentity Unknown(string source) => new("", "", "", "", source);
}

internal sealed class HardwareIdentityReader(
    IFileSystem fileSystem,
    ICommandRunner commandRunner)
{
    public HardwareIdentity Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxHardwareIdentityProvider(fileSystem).Read();

        if (OperatingSystem.IsMacOS())
            return new MacHardwareIdentityProvider(commandRunner).Read();

        return HardwareIdentity.Unknown("runtime");
    }
}

internal sealed class LinuxHardwareIdentityProvider(IFileSystem fileSystem)
{
    private const string DmiRoot = "/sys/class/dmi/id";

    public HardwareIdentity Read()
    {
        var vendor = ReadDmi("sys_vendor");
        var productName = ReadDmi("product_name");
        var productVersion = ReadDmi("product_version");
        var boardVendor = ReadDmi("board_vendor");
        var boardName = ReadDmi("board_name");
        var serial = ReadDmi("product_serial");

        var resolvedVendor = FirstPresent(vendor, boardVendor);
        var resolvedProduct = FirstPresent(productName, boardName);
        var model = JoinPresent(" ", productName, productVersion);

        return new HardwareIdentity(
            resolvedVendor,
            string.IsNullOrWhiteSpace(model) ? resolvedProduct : model,
            resolvedProduct,
            serial,
            "linux-dmi");
    }

    private string ReadDmi(string fileName) =>
        Normalize(fileSystem.ReadAllText($"{DmiRoot}/{fileName}"));

    private static string Normalize(string value)
    {
        var trimmed = value.Trim();
        return IsPlaceholder(trimmed) ? string.Empty : trimmed;
    }

    private static bool IsPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals("None", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Not Specified", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("System Product Name", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Default string", StringComparison.OrdinalIgnoreCase);

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string JoinPresent(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}

internal sealed class MacHardwareIdentityProvider(ICommandRunner commandRunner)
{
    public HardwareIdentity Read()
    {
        var modelIdentifier = commandRunner.Run("sysctl", "-n", "hw.model").Trim();
        var systemProfiler = commandRunner.Run("system_profiler", "SPHardwareDataType");
        var modelName = ExtractSystemProfilerValue(systemProfiler, "Model Name");
        var serial = ExtractSystemProfilerValue(systemProfiler, "Serial Number");
        var chip = ExtractSystemProfilerValue(systemProfiler, "Chip");
        var processor = ExtractSystemProfilerValue(systemProfiler, "Processor Name");

        var model = JoinPresent(" ", modelName, modelIdentifier);
        if (string.IsNullOrWhiteSpace(model))
            model = FirstPresent(modelIdentifier, chip, processor);

        return new HardwareIdentity(
            "Apple Inc.",
            model,
            FirstPresent(modelName, modelIdentifier),
            serial,
            "macos-system-profiler");
    }

    private static string ExtractSystemProfilerValue(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"^\s*{Regex.Escape(key)}(?:\s*\(.+?\))?:\s*(?<value>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string JoinPresent(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
}

internal interface IFileSystem
{
    string ReadAllText(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);

    bool DirectoryExists(string path);

    string GetFileName(string path);
}

internal sealed class PhysicalFileSystem : IFileSystem
{
    public string ReadAllText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateDirectories(path).ToArray() : [];
        }
        catch
        {
            return [];
        }
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateFiles(path, searchPattern).ToArray() : [];
        }
        catch
        {
            return [];
        }
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public string GetFileName(string path) => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}

internal interface ICommandRunner
{
    string Run(string fileName, params string[] arguments);
}

internal interface ICommandResultRunner : ICommandRunner
{
    CommandResult RunResult(string fileName, params string[] arguments);
}

internal sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    public string GetSummary()
    {
        var output = string.Join(
            " ",
            new[] { StandardOutput, StandardError }
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(output) ? $"exit code {ExitCode}" : output;
    }
}

internal sealed class ProcessCommandRunner : ICommandResultRunner
{
    public string Run(string fileName, params string[] arguments)
    {
        var result = RunResult(fileName, arguments);
        return result.Succeeded ? result.StandardOutput : string.Empty;
    }

    public CommandResult RunResult(string fileName, params string[] arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }.WithArguments(arguments));

            if (process is null)
                return new CommandResult(-1, string.Empty, "Process could not be started.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);

                // After Kill the ReadToEndAsync tasks should complete, but guard
                // against a hung pipe with a secondary timeout to avoid deadlocking
                // the caller on GetAwaiter().GetResult().
                if (!Task.WhenAll(outputTask, errorTask).Wait(2000))
                    return new CommandResult(-1, string.Empty, "Process timed out and output could not be read.");

                return new CommandResult(-1, string.Empty, "Process timed out.");
            }

            // Process exited normally — wait for output with a generous timeout
            // to avoid blocking forever on a stuck pipe.
            if (!Task.WhenAll(outputTask, errorTask).Wait(5000))
                return new CommandResult(-1, string.Empty, "Process exited but output could not be read.");

            var output = outputTask.Result;
            var error = errorTask.Result;
            return new CommandResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, string.Empty, ex.Message);
        }
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArguments(this ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }
}
