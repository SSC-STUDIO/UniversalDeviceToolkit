using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UniversalDeviceToolkit.Shared.Settings;

namespace UniversalDeviceToolkit.Lib.Features.CursorPointer;

/// <summary>
/// Persisted state for the cursor &amp; pointer feature (cursorPointer.json).
/// Mirrors the flat keys the retired custom-mouse plugin kept in its plugin-scoped
/// config.json so a one-time legacy import can adopt existing user state, including
/// the backed-up original cursor scheme used to undo UDT cursor themes.
/// </summary>
public class CursorPointerSettings() : AbstractSettings<CursorPointerSettings.Data>("cursorPointer.json")
{
    public class Data
    {
        public int WindowsPointerSpeed { get; set; } = 10;
        public bool SwapButtons { get; set; }
        public bool AutoThemeCursorStyle { get; set; } = true;
        /// <summary>Persisted as raw int (see <see cref="CursorThemeMode"/>); 0 = Auto.</summary>
        public int CursorThemeMode { get; set; }
        public string LastAppliedTheme { get; set; } = string.Empty;

        /// <summary>True once the pre-UDT cursor scheme has been captured.</summary>
        public bool CursorBackupSaved { get; set; }

        /// <summary>Registry default value ("") of Control Panel\Cursors when first modified.</summary>
        public string CursorBackupDefault { get; set; } = string.Empty;

        /// <summary>Per-value-name mirror of Control Panel\Cursors before first modification.</summary>
        public Dictionary<string, string> CursorBackup { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Set after the one-time import of the retired plugin's config.json.</summary>
        public bool LegacyImportDone { get; set; }
    }

    protected override Data Default => new()
    {
        WindowsPointerSpeed = 10,
        SwapButtons = false,
        AutoThemeCursorStyle = true,
        CursorThemeMode = (int)CursorThemeMode.Auto,
        LastAppliedTheme = string.Empty
    };

    /// <summary>
    /// One-time best-effort adoption of the retired custom-mouse plugin's config.json
    /// (flat key/value JSON under %LocalAppData%\UniversalDeviceToolkit\plugins\custom-mouse).
    /// Key lookup is tolerant of missing files or unknown shapes.
    /// </summary>
    public override Data? LoadStore()
    {
        var store = base.LoadStore();
        if (store is null)
            return null;

        if (store.LegacyImportDone || TryImportLegacyPluginConfig(store))
            return store;

        store.LegacyImportDone = true;
        SynchronizeStore();
        return store;
    }

    private static bool TryImportLegacyPluginConfig(Data store)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UniversalDeviceToolkit", "plugins", "custom-mouse", "config.json");

            if (!File.Exists(path))
                return false;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            int GetInt(string key, int fallback) =>
                document.RootElement.TryGetProperty(key, out var value) && value.TryGetInt32(out var raw)
                    ? raw
                    : fallback;

            bool GetBool(string key, bool fallback) =>
                document.RootElement.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? value.GetBoolean()
                    : fallback;

            string GetString(string key) =>
                document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : string.Empty;

            store.WindowsPointerSpeed = Math.Clamp(GetInt("WindowsPointerSpeed", store.WindowsPointerSpeed), 1, 20);
            store.SwapButtons = GetBool("SwapButtons", store.SwapButtons);
            store.AutoThemeCursorStyle = GetBool("AutoThemeCursorStyle", store.AutoThemeCursorStyle);
            store.CursorThemeMode = GetInt("CursorThemeMode", store.CursorThemeMode);
            store.LastAppliedTheme = GetString("LastAppliedTheme");
            store.CursorBackupSaved = GetBool("CursorBackupSaved", store.CursorBackupSaved);
            store.CursorBackupDefault = GetString("CursorBackup_Default");

            foreach (var property in document.RootElement.EnumerateObject())
            {
                const string prefix = "CursorBackup_";
                if (property.Name.Length <= prefix.Length
                    || !property.Name.StartsWith(prefix, StringComparison.Ordinal)
                    || property.Name == "CursorBackup_Default"
                    || property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                store.CursorBackup[property.Name[prefix.Length..]] = property.Value.GetString() ?? string.Empty;
            }

            return true;
        }
        catch (Exception ex)
        {
            Shared.Logging.SharedLog.Trace("CursorPointer: legacy plugin config import failed.", ex);
            return false;
        }
    }
}

