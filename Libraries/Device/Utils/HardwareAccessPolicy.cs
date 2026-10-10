using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Keeps lazy native hardware access disabled during isolated UI diagnostics.
/// </summary>
internal static class HardwareAccessPolicy
{
    private static readonly string[] ProcessArguments = Environment.GetCommandLineArgs();

    internal static bool IsDisabled => ShouldDisableHardware(
        ProcessArguments,
        Environment.GetEnvironmentVariable("UDT_DIAGNOSTIC_MODE"));

    internal static bool ShouldDisableHardware(IReadOnlyList<string> arguments, string? diagnosticMode) =>
        diagnosticMode == "1" || arguments.Contains("--no-hardware", StringComparer.Ordinal);
}
