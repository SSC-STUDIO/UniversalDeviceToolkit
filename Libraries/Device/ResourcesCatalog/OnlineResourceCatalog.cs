using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.ResourcesCatalog;

public sealed record OnlineResourceCatalog
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; init; } = string.Empty;

    [JsonPropertyName("languages")]
    public IReadOnlyCollection<OnlineLanguageResource> Languages { get; init; } = [];

    [JsonPropertyName("devicePacks")]
    public IReadOnlyCollection<OnlineDevicePackResource> DevicePacks { get; init; } = [];

    [JsonPropertyName("downloads")]
    public OnlineDownloads? Downloads { get; init; }

    [JsonPropertyName("sha256")]
    public OnlineFileResource? Sha256 { get; init; }
}

public sealed record OnlineDownloads
{
    [JsonPropertyName("full")]
    public OnlineDownloadGroup? Full { get; init; }

    [JsonPropertyName("online")]
    public OnlineDownloadGroup? Online { get; init; }

    [JsonPropertyName("cli")]
    public OnlineCliDownloadGroup? Cli { get; init; }
}

public sealed record OnlineDownloadGroup
{
    [JsonPropertyName("portable")]
    public OnlineFileResource? Portable { get; init; }

    [JsonPropertyName("installer")]
    public OnlineFileResource? Installer { get; init; }
}

public sealed record OnlineCliDownloadGroup
{
    [JsonPropertyName("crossPlatform")]
    public OnlineFileResource? CrossPlatform { get; init; }
}

public sealed record OnlineFileResource
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}

public sealed record OnlineLanguageResource
{
    [JsonPropertyName("culture")]
    public string Culture { get; init; } = string.Empty;

    [JsonPropertyName("parent")]
    public string? Parent { get; init; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("resourceVersion")]
    public string? ResourceVersion { get; init; }

    [JsonPropertyName("minAppVersion")]
    public string? MinAppVersion { get; init; }
}

public sealed record OnlineDevicePackResource
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("vendor")]
    public string Vendor { get; init; } = string.Empty;

    [JsonPropertyName("vendorAliases")]
    public IReadOnlyCollection<string> VendorAliases { get; init; } = [];

    [JsonPropertyName("families")]
    public IReadOnlyCollection<string> Families { get; init; } = [];

    [JsonPropertyName("modelPrefixes")]
    public IReadOnlyCollection<string> ModelPrefixes { get; init; } = [];

    [JsonPropertyName("machineTypes")]
    public IReadOnlyCollection<string> MachineTypes { get; init; } = [];

    [JsonPropertyName("modelKeywords")]
    public IReadOnlyCollection<string> ModelKeywords { get; init; } = [];

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}
