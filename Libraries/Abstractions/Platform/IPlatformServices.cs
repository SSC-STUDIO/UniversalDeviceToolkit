// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Unified abstraction for platform-specific hardware access.
/// Each platform (Windows, macOS, Linux) provides its own implementation.
/// </summary>
public interface IPlatformServices
{
    /// <summary>Gets the platform identifier (windows, linux, macos).</summary>
    string PlatformName { get; }
    
    /// <summary>Checks if the current platform supports GPU management.</summary>
    bool SupportsGpuManagement { get; }
    
    /// <summary>Checks if the current platform supports fan control.</summary>
    bool SupportsFanControl { get; }
    
    /// <summary>Checks if the current platform supports keyboard backlight.</summary>
    bool SupportsKeyboardBacklight { get; }

    /// <summary>Checks if the current platform supports battery management.</summary>
    bool SupportsBatteryManagement { get; }

    /// <summary>Checks if the current platform supports display control (brightness, refresh rate).</summary>
    bool SupportsDisplayControl { get; }

    /// <summary>Checks if the current platform supports power profile management.</summary>
    bool SupportsPowerProfile { get; }

    /// <summary>Checks if the current platform supports system telemetry (CPU/GPU/memory stats).</summary>
    bool SupportsSystemTelemetry { get; }
}
