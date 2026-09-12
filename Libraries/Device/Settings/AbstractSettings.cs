namespace UniversalDeviceToolkit.Lib.Settings;

/// <summary>Windows compatibility facade over the shared settings implementation.</summary>
public abstract class AbstractSettings<T> : UniversalDeviceToolkit.Shared.Settings.AbstractSettings<T>
    where T : class, new()
{
    protected AbstractSettings(string filename) : base(filename) { }
}
