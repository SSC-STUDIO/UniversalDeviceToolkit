using SharedAppIdentity = UniversalDeviceToolkit.Shared.Utils.AppIdentity;

namespace UniversalDeviceToolkit.Lib.Utils;

// Thin ABI-compatible wrapper over UniversalDeviceToolkit.Shared.Utils.AppIdentity
// (single source of truth). Constants are forwarded so existing callers keep
// compiling against the original Lib namespace.
public static class AppIdentity
{
    public const string DisplayName = SharedAppIdentity.DisplayName;
    public const string CompactName = SharedAppIdentity.CompactName;
    public const string LegacyDisplayName = SharedAppIdentity.LegacyDisplayName;
    public const string LegacyCompactName = SharedAppIdentity.LegacyCompactName;

    public const string Publisher = SharedAppIdentity.Publisher;
    public const string RepositoryOwner = SharedAppIdentity.RepositoryOwner;
    public const string RepositoryName = SharedAppIdentity.RepositoryName;
    public const string LegacyRepositoryName = SharedAppIdentity.LegacyRepositoryName;
    public const string RepositoryUrl = SharedAppIdentity.RepositoryUrl;
    public const string LegacyRepositoryUrl = SharedAppIdentity.LegacyRepositoryUrl;

    public const string ResourcesBaseUrl = SharedAppIdentity.ResourcesBaseUrl;
    public const string StableResourceCatalogUrl = SharedAppIdentity.StableResourceCatalogUrl;
}
