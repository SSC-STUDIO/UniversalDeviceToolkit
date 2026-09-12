using System;

namespace UniversalDeviceToolkit.Shared.Utils;

/// <summary>
/// Simplified cross-platform exception factories.
/// Extracted from Lib.Utils.ExceptionHelper — only settings-related methods included;
/// Windows-specific factories (registry, driver, WMI, etc.) intentionally omitted.
/// </summary>
public static class ExceptionHelper
{
    public static ArgumentException InvalidSettingsFilename(string filename, string paramName) =>
        new($"Invalid settings filename: '{filename}'.", paramName);

    public static InvalidOperationException SettingsPathEscapesAllowedDir(string settingsStorePath) =>
        new($"Settings path '{settingsStorePath}' escapes the allowed application data directory.");
}
