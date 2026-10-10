using System.Globalization;
using System.Resources;

namespace UniversalDeviceToolkit.Abstractions.Localization;

/// <summary>
/// ResourceManager-backed implementation with the shared UDT fallback policy.
/// </summary>
public sealed class ResourceManagerStringLocalizer : IStringLocalizer
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public ResourceManagerStringLocalizer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        _currentCulture = CultureInfo.CurrentUICulture;
    }

    public string GetString(string key, string fallback = "") =>
        LocalizationCatalog.GetString(_resourceManager, key, fallback, CurrentCulture);

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set => _currentCulture = value ?? throw new ArgumentNullException(nameof(value));
    }
}
