using System.IO;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Security)]
public sealed class OnlineCatalogHostGuardTests
{
    [Fact]
    public void ResourceCatalogOverride_ShouldUseAnExplicitHostAllowlist()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryPaths.FindRoot(),
            "Libraries/Device",
            "ResourcesCatalog",
            "OnlineResourceCatalogClient.cs"));

        source.Should().Contain("AllowedCatalogHosts");
        source.Should().Contain("uri.Scheme != Uri.UriSchemeHttps");
        source.Should().Contain("IsAllowedCatalogHost(uri.Host)");
        source.Should().Contain("ssc-studio.github.io");
        source.Should().Contain("cdn.jsdelivr.net");
        source.Should().Contain("github.com");
        source.Should().Contain("raw.githubusercontent.com");
        source.Should().Contain("gh-proxy.com");
        source.Should().Contain("ghfast.top");
    }

    [Fact]
    public void ResourceCatalogOverride_ShouldKeepTestHostBehindTestHooks()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryPaths.FindRoot(),
            "Libraries/Device",
            "ResourcesCatalog",
            "OnlineResourceCatalogClient.cs"));

        source.Should().Contain("#if UDT_TEST_HOOKS");
        source.Should().Contain("example.test");
    }
}
