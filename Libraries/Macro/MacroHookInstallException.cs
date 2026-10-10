using System;

namespace UniversalDeviceToolkit.Lib.Macro;

/// <summary>
/// Thrown when the playback WH_KEYBOARD_LL hook cannot be installed on the
/// dedicated message-pump thread (SetWindowsHookEx failed or timed out).
/// </summary>
public sealed class MacroHookInstallException : InvalidOperationException
{
    public MacroHookInstallException(string message)
        : base(message)
    {
    }
}
