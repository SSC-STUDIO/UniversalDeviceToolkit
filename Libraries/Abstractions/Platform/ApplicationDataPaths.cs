namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Canonical application-data root shared by localization, settings, and diagnostics.
/// Test builds may redirect the root through <see cref="OverrideEnvironmentVariable"/>.
/// </summary>
public static class ApplicationDataPaths
{
    public const string DirectoryName = "UniversalDeviceToolkit";

    public static string OverrideEnvironmentVariable => string.Concat("UDT", "_APPDATA", "_OVERRIDE");

    public static bool IsOverridden
    {
        get
        {
#if UDT_TEST_HOOKS
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OverrideEnvironmentVariable));
#else
            return false;
#endif
        }
    }

    public static string GetRoot()
    {
#if UDT_TEST_HOOKS
        var overridePath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);
#endif

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
