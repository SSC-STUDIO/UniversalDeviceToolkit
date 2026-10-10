using System;

namespace UniversalDeviceToolkit.Lib.Automation;

/// <summary>
/// Automation data can be loaded without constructing its optional hardware.
/// Execution reports a missing service explicitly when a restricted host
/// cannot provide the saved step's backend.
/// </summary>
internal static class AutomationServiceResolver
{
    internal static T ResolveRequired<T>() where T : class => IoCContainer.TryResolve<T>()
        ?? throw new NotSupportedException($"NOT_SUPPORTED: {typeof(T).Name} is unavailable in this host session.");
}
