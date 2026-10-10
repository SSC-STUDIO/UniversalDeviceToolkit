using Microsoft.Win32;
using UniversalDeviceToolkit.Abstractions.Lifecycle;

namespace UniversalDeviceToolkit.Platform.Windows.Lifecycle;

public sealed class WindowsAutorunManager : IAutorunManager
{
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "UniversalDeviceToolkit";

    public Task<bool> IsEnabledAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        var value = key?.GetValue(EntryName);
        return Task.FromResult(value is not null);
    }

    public Task EnableAsync()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is null) return Task.CompletedTask;

        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue(EntryName, $"\"{exePath}\"", RegistryValueKind.String);
        return Task.CompletedTask;
    }

    public Task DisableAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
        key?.DeleteValue(EntryName, throwOnMissingValue: false);
        return Task.CompletedTask;
    }
}
