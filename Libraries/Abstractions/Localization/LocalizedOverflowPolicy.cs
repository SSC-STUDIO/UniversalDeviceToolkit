namespace UniversalDeviceToolkit.Abstractions.Localization;

/// <summary>
/// Shared layout budget for localized text across the WPF hosts.
/// </summary>
public static class LocalizedOverflowPolicy
{
    public const int TitleMaxLines = 2;
    public const int DescriptionMaxLines = 3;
    public const double MinimumReadableFontSize = 11.0;

    public const LocalizedOverflowMode TitleMode = LocalizedOverflowMode.Wrap;
    public const LocalizedOverflowMode DescriptionMode = LocalizedOverflowMode.Wrap;
    public const LocalizedOverflowMode CompactMode = LocalizedOverflowMode.Ellipsis;

    public static int GetMaxLines(LocalizedOverflowMode mode) => mode switch
    {
        LocalizedOverflowMode.Wrap => DescriptionMaxLines,
        _ => 1,
    };
}
