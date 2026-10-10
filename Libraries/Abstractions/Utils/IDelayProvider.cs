// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Abstractions.Utils;

/// <summary>
/// Cross-platform delay provider for async operations.
/// </summary>
public interface IDelayProvider
{
    /// <summary>
    /// Delays execution for the specified time span.
    /// </summary>
    Task Delay(TimeSpan delay, CancellationToken token);
}
