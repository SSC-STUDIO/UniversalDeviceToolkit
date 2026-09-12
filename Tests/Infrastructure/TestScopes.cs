using System.Globalization;

namespace UniversalDeviceToolkit.Tests;

/// <summary>Restores a process environment variable when a test scope ends.</summary>
public sealed class EnvironmentVariableScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;
    private bool _disposed;

    public EnvironmentVariableScope(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        _previousValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Environment.SetEnvironmentVariable(_name, _previousValue);
        _disposed = true;
    }
}

/// <summary>Restores the current execution context cultures when a test scope ends.</summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCurrentCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousCurrentUiCulture = CultureInfo.CurrentUICulture;
    private bool _disposed;

    public CultureScope(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CultureInfo.CurrentCulture = _previousCurrentCulture;
        CultureInfo.CurrentUICulture = _previousCurrentUiCulture;
        _disposed = true;
    }
}
