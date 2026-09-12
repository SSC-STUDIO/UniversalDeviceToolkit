namespace UniversalDeviceToolkit.Tests;

/// <summary>
/// Named xUnit collections. Use DisableParallelization for tests that mutate
/// process-wide state (UI culture, Resource.Culture, shared settings files).
/// </summary>
public static class TestCollections
{
    public const string Localization = "Localization Tests";
    public const string Settings = "Settings Tests";
    public const string ProcessState = "Process State Tests";
}
