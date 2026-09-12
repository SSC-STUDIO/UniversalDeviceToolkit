namespace UniversalDeviceToolkit.Abstractions.Localization;

/// <summary>
/// Describes how a localized value should fit inside its semantic UI slot.
/// </summary>
public enum LocalizedOverflowMode
{
    /// <summary>Allow the value to flow onto a bounded number of lines.</summary>
    Wrap,

    /// <summary>Keep the value compact and render an ellipsis when it does not fit.</summary>
    Ellipsis,
}
