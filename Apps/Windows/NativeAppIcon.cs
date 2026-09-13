using System.ComponentModel;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Owns the application icons extracted from the shipping apphost.</summary>
internal sealed class NativeAppIcon : IDisposable
{
    private bool _disposed;
    internal nint Large { get; }
    internal nint Small { get; }

    internal NativeAppIcon()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "UniversalDeviceToolkit.exe");
        var count = Win32.ExtractIconEx(executable, 0, out var large, out var small, 1);
        Large = large;
        Small = small;
        if (count == 0 || large == 0 || small == 0)
        {
            Dispose();
            throw new Win32Exception("The application icon could not be loaded from the bundled executable.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Large != 0) Win32.DestroyIcon(Large);
        if (Small != 0 && Small != Large) Win32.DestroyIcon(Small);
    }
}
