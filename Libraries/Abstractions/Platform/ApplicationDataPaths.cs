namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Canonical application-data root shared by localization, settings, and diagnostics.
/// Test builds and explicit diagnostic sessions may redirect the data root.
/// </summary>
public static class ApplicationDataPaths
{
    public const string DirectoryName = "UniversalDeviceToolkit";

    public static string OverrideEnvironmentVariable => string.Concat("UDT", "_APPDATA", "_OVERRIDE");

    private static bool SupportsOverride
    {
        get
        {
#if UDT_TEST_HOOKS
            return true;
#else
            return Environment.GetEnvironmentVariable("UDT_DIAGNOSTIC_MODE") == "1";
#endif
        }
    }

    public static bool IsOverridden
    {
        get
        {
            return SupportsOverride && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OverrideEnvironmentVariable));
        }
    }

    public static string GetRoot()
    {
        var overridePath = SupportsOverride ? Environment.GetEnvironmentVariable(OverrideEnvironmentVariable) : null;
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DirectoryName);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configHome, DirectoryName);
    }
}
