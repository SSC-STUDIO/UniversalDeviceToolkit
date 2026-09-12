using System.Diagnostics;
using System.Linq;
using System.Reflection;

internal sealed record ElevationLaunchResult(
    bool Succeeded,
    string Detail);

internal sealed record ElevationLaunchPlan(
    string FileName,
    string[] Arguments,
    string WorkingDirectory);

internal readonly record struct ElevationEnvironmentSnapshot(
    string? ProcessPath,
    string? EntryAssemblyLocation,
    string CurrentDirectory,
    string SystemDirectory,
    string? DotNetHostPath,
    string? DotNetRoot,
    string? ProgramFiles,
    string? ProgramFilesX86,
    Func<string, bool> FileExists,
    Func<string, bool> DirectoryExists)
{
    public static ElevationEnvironmentSnapshot Capture() => new(
        Environment.ProcessPath,
        Assembly.GetEntryAssembly()?.Location,
        Environment.CurrentDirectory,
        Environment.SystemDirectory,
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),
        Environment.GetEnvironmentVariable("DOTNET_ROOT"),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        File.Exists,
        Directory.Exists);
}

internal sealed class ElevationLauncher
{
    private readonly ElevationEnvironmentSnapshot _environment;

    public ElevationLauncher()
        : this(ElevationEnvironmentSnapshot.Capture())
    {
    }

    internal ElevationLauncher(ElevationEnvironmentSnapshot environment)
    {
        _environment = environment;
    }

    public ElevationLaunchResult Launch(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return new ElevationLaunchResult(
                false,
                "Usage: udt elevate <command> [arguments]. Example: udt elevate set cpu-governor performance");
        }

        if (!OperatingSystem.IsWindows())
        {
            return new ElevationLaunchResult(
                false,
                "UAC elevation is available on Windows only. On Linux/macOS, rerun the command with sudo or the platform polkit helper when the OS requires elevated hardware-write permissions.");
        }

        foreach (var arg in arguments)
        {
            if (!IsSafeArgument(arg))
            {
                return new ElevationLaunchResult(
                    false,
                    $"Argument contains unsafe characters: '{arg}'. Rejected for security.");
            }
        }

        if (!TryBuildWindowsLaunchPlan(arguments, _environment, out var plan, out var planError))
        {
            return new ElevationLaunchResult(false, planError);
        }

        foreach (var arg in plan.Arguments)
        {
            if (!IsSafeArgument(arg))
            {
                return new ElevationLaunchResult(
                    false,
                    $"Argument contains unsafe characters: '{arg}'. Rejected for security.");
            }
        }

        try
        {
            // UseShellExecute MUST be true for Verb="runas" to trigger the UAC prompt.
            // With UseShellExecute=false the Verb is silently ignored and the process
            // starts without elevation — the exact defect reported at line 41.
            var argumentString = string.Join(" ",
                plan.Arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

            var startInfo = new ProcessStartInfo(plan.FileName)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = plan.WorkingDirectory,
                Arguments = argumentString
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ElevationLaunchResult(
                    false,
                    "Process.Start returned null — UAC elevation was denied or failed silently.");
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return new ElevationLaunchResult(
                    false,
                    $"Elevated process exited with code {process.ExitCode}.");
            }

            return new ElevationLaunchResult(true, "Started the requested command.");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED — user clicked "No" on the UAC prompt.
            return new ElevationLaunchResult(false, "UAC elevation was cancelled by the user.");
        }
        catch (Exception ex)
        {
            return new ElevationLaunchResult(false, $"Could not start the elevated command: {ex.Message}");
        }
    }

    internal static bool TryBuildWindowsLaunchPlan(
        IReadOnlyList<string> arguments,
        ElevationEnvironmentSnapshot environment,
        out ElevationLaunchPlan plan,
        out string error)
    {
        plan = new ElevationLaunchPlan(string.Empty, [], string.Empty);
        error = string.Empty;

        if (arguments.Count == 0)
        {
            error = "Usage: udt elevate <command> [arguments]. Example: udt elevate set cpu-governor performance";
            return false;
        }

        var processPath = environment.ProcessPath;
        if (IsNativeAppHost(processPath))
        {
            if (!TryVerifyTrustedExecutable(
                    processPath,
                    Path.GetFileName(processPath),
                    environment.CurrentDirectory,
                    rejectCurrentDirectory: false,
                    environment.FileExists,
                    out var verifiedAppHost,
                    out error))
            {
                return false;
            }

            var appHostWorkingDirectory = ResolveSafeWorkingDirectory(
                environment,
                Path.GetDirectoryName(verifiedAppHost));
            plan = new ElevationLaunchPlan(verifiedAppHost, arguments.ToArray(), appHostWorkingDirectory);
            return true;
        }

        if (!TryResolveTrustedDotNetHost(environment, out var verifiedDotNet, out error))
            return false;

        if (!TryResolveEntryAssemblyDll(environment, out var entryDll, out error))
            return false;

        var workingDirectory = ResolveSafeWorkingDirectory(
            environment,
            Path.GetDirectoryName(verifiedDotNet));
        plan = new ElevationLaunchPlan(verifiedDotNet, [entryDll, .. arguments], workingDirectory);
        return true;
    }

    internal static bool TryResolveTrustedDotNetHost(
        ElevationEnvironmentSnapshot environment,
        out string verifiedPath,
        out string error)
    {
        string? lastError = null;
        foreach (var candidate in EnumerateDotNetHostCandidates(environment))
        {
            if (TryVerifyTrustedExecutable(
                    candidate,
                    expectedFileName: null,
                    environment.CurrentDirectory,
                    rejectCurrentDirectory: true,
                    environment.FileExists,
                    out verifiedPath,
                    out var candidateError))
            {
                if (IsDotNetHostFileName(verifiedPath))
                {
                    error = string.Empty;
                    return true;
                }

                lastError = "Resolved host file name must be dotnet or dotnet.exe.";
                continue;
            }

            lastError = candidateError;
        }

        verifiedPath = string.Empty;
        error = lastError ?? "Could not resolve a trusted absolute dotnet host for UAC elevation.";
        return false;
    }

    internal static bool TryVerifyTrustedExecutable(
        string? candidatePath,
        string? expectedFileName,
        string currentDirectory,
        bool rejectCurrentDirectory,
        Func<string, bool> fileExists,
        out string verifiedPath,
        out string error)
    {
        verifiedPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            error = "Host path is empty.";
            return false;
        }

        if (candidatePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "Host path contains invalid characters.";
            return false;
        }

        if (!Path.IsPathRooted(candidatePath))
        {
            error = "Host path must be an absolute path.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidatePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            error = $"Host path could not be normalized: {ex.Message}";
            return false;
        }

        if (!Path.IsPathRooted(fullPath))
        {
            error = "Host path must be an absolute path.";
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "Host path does not include a file name.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedFileName) &&
            !fileName.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Host file name must be '{expectedFileName}'.";
            return false;
        }

        if (expectedFileName is null && !IsDotNetHostFileName(fullPath))
        {
            error = "Host file name must be dotnet or dotnet.exe.";
            return false;
        }

        if (!fileExists(fullPath))
        {
            error = "Host path does not exist.";
            return false;
        }

        if (rejectCurrentDirectory)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) &&
                AreSameDirectory(directory, currentDirectory))
            {
                error = "Host path in the current directory is not trusted for UAC elevation.";
                return false;
            }
        }

        verifiedPath = fullPath;
        return true;
    }

    internal static string ResolveSafeWorkingDirectory(
        ElevationEnvironmentSnapshot environment,
        string? hostDirectory)
    {
        if (TryCanonicalExistingDirectory(environment.SystemDirectory, environment.DirectoryExists, out var systemDirectory))
            return systemDirectory;

        if (TryCanonicalExistingDirectory(hostDirectory, environment.DirectoryExists, out var resolvedHostDirectory))
            return resolvedHostDirectory;

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    internal static bool IsSafeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return false;

        foreach (var c in arg)
        {
            if (char.IsControl(c))
                return false;

            switch (c)
            {
                case '&':
                case '|':
                case ';':
                case '$':
                case '`':
                case '(':
                case ')':
                case '<':
                case '>':
                case '#':
                case '@':
                case '!':
                case '~':
                case '{':
                case '}':
                case '[':
                case ']':
                case '*':
                case '?':
                    return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> EnumerateDotNetHostCandidates(ElevationEnvironmentSnapshot environment)
    {
        if (!string.IsNullOrWhiteSpace(environment.DotNetHostPath))
            yield return environment.DotNetHostPath;

        if (IsDotNetHostFileName(environment.ProcessPath) && !string.IsNullOrWhiteSpace(environment.ProcessPath))
            yield return environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(environment.DotNetRoot))
            yield return Path.Combine(environment.DotNetRoot, "dotnet.exe");

        if (!string.IsNullOrWhiteSpace(environment.ProgramFiles))
            yield return Path.Combine(environment.ProgramFiles, "dotnet", "dotnet.exe");

        if (!string.IsNullOrWhiteSpace(environment.ProgramFilesX86))
            yield return Path.Combine(environment.ProgramFilesX86, "dotnet", "dotnet.exe");
    }

    private static bool TryResolveEntryAssemblyDll(
        ElevationEnvironmentSnapshot environment,
        out string entryDll,
        out string error)
    {
        entryDll = string.Empty;
        error = string.Empty;

        var location = environment.EntryAssemblyLocation;
        if (string.IsNullOrWhiteSpace(location) ||
            !string.Equals(Path.GetExtension(location), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            error = "Entry assembly DLL could not be resolved for framework-dependent elevation.";
            return false;
        }

        if (!Path.IsPathRooted(location))
        {
            error = "Entry assembly path must be absolute.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(location);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            error = $"Entry assembly path could not be normalized: {ex.Message}";
            return false;
        }

        if (!environment.FileExists(fullPath))
        {
            error = "Entry assembly DLL does not exist.";
            return false;
        }

        entryDll = fullPath;
        return true;
    }

    private static bool IsNativeAppHost(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return false;

        if (IsDotNetHostFileName(processPath))
            return false;

        return string.Equals(Path.GetExtension(processPath), ".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDotNetHostFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var name = Path.GetFileName(path);
        return name.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCanonicalExistingDirectory(
        string? path,
        Func<string, bool> directoryExists,
        out string directory)
    {
        directory = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!directoryExists(fullPath))
                return false;

            directory = fullPath;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    private static bool AreSameDirectory(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            var leftFull = Path.GetFullPath(left)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rightFull = Path.GetFullPath(right)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return leftFull.Equals(rightFull, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }
}
