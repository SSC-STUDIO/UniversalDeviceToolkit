using Microsoft.Win32;
using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Platform.Windows.Platform;

public sealed class WindowsConfigurationStore : IConfigurationStore
{
    private const string RootPath = @"SOFTWARE\UniversalDeviceToolkit";

    public string? GetValue(string section, string key)
    {
        using var subKey = Registry.CurrentUser.OpenSubKey($@"{RootPath}\{section}");
        return subKey?.GetValue(key) as string;
    }

    public void SetValue(string section, string key, string? value)
    {
        if (value is null)
        {
            using var subKey = Registry.CurrentUser.OpenSubKey($@"{RootPath}\{section}", writable: true);
            subKey?.DeleteValue(key, throwOnMissingValue: false);
        }
        else
        {
            using var subKey = Registry.CurrentUser.CreateSubKey($@"{RootPath}\{section}");
            subKey.SetValue(key, value, RegistryValueKind.String);
        }
    }

    public IReadOnlyDictionary<string, string> GetSection(string section)
    {
        using var subKey = Registry.CurrentUser.OpenSubKey($@"{RootPath}\{section}");
        if (subKey is null) return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();
        foreach (var name in subKey.GetValueNames())
        {
            if (subKey.GetValue(name) is string val)
            {
                result[name] = val;
            }
        }
        return result;
    }
}
