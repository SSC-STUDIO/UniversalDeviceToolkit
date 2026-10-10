namespace UniversalDeviceToolkit.Tests;

/// <summary>
/// Locates the repository root for guard tests that read source/CI files.
/// Works under VSTest (BaseDirectory = test output) and external runners
/// (xunit.console BaseDirectory = runner install path).
/// </summary>
public static class RepositoryPaths
{
    private const string SolutionFileName = "UniversalDeviceToolkit.sln";

    public static string FindRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("UDT_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot) &&
            File.Exists(Path.Combine(overrideRoot, SolutionFileName)))
        {
            return Path.GetFullPath(overrideRoot);
        }

        foreach (var start in GetSearchRoots())
        {
            if (string.IsNullOrWhiteSpace(start))
                continue;

            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find repository root ({SolutionFileName}). " +
            "Set UDT_REPOSITORY_ROOT or run tests from the repository tree.");
    }

    public static string Combine(params string[] pathParts) =>
        Path.Combine([FindRoot(), .. pathParts]);

    public static string ReadFile(params string[] pathParts) =>
        File.ReadAllText(Combine(pathParts));

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return AppContext.BaseDirectory;

        var assemblyLocation = typeof(RepositoryPaths).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var directory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(directory))
                yield return directory;
        }

        yield return Environment.CurrentDirectory;
    }
}
