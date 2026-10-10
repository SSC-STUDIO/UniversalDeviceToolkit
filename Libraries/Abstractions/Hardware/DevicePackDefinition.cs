namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Data-only device-pack definition. The JSON property shape is shared with the
/// Windows device-pack catalog and can be consumed by portable clients.
/// </summary>
public sealed record DevicePackDefinition
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Vendor { get; init; } = string.Empty;
    public IReadOnlyCollection<string> VendorAliases { get; init; } = [];
    public IReadOnlyCollection<string> Families { get; init; } = [];
    public IReadOnlyCollection<string> ModelPrefixes { get; init; } = [];
    public IReadOnlyCollection<string> ModelKeywords { get; init; } = [];
    public IReadOnlyCollection<string> MachineTypes { get; init; } = [];
    public IReadOnlyCollection<string> EnabledFeatures { get; init; } = [];
    public IReadOnlyCollection<string> HiddenFeatures { get; init; } = [];
}
