using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class BuiltinDomainGroupsDefaultsTests
{
    private static readonly string[] PublicCdnHostnames =
    [
        "translate.googleapis.com",
        "open.spotify.com",
        "fonts.gstatic.com",
        "gravatar.com",
        "themes.googleusercontent.com",
        "ajax.googleapis.com",
        "fonts.googleapis.com",
        "maxcdn.bootstrapcdn.com",
        "cdn.jsdelivr.net",
        "cdnjs.cloudflare.com",
        "unpkg.com"
    ];

    [Fact]
    public void CreateDefaults_DoesNotContainCustomGroup()
    {
        var groups = BuiltinDomainGroups.CreateDefaults();
        groups.Should().NotContain(g => string.Equals(g.Id, "custom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateDefaults_PublicCdn_HasExpandedSubItems()
    {
        var cdn = BuiltinDomainGroups.CreateDefaults().First(g => g.Id == "public-cdn");
        cdn.SubItems.Should().HaveCount(9);
        cdn.SubItems.Should().Contain(s => s.Id == "cdn-jsdelivr");
        cdn.SubItems.Should().Contain(s => s.Id == "cdn-cdnjs");
        cdn.SubItems.Should().Contain(s => s.Id == "cdn-unpkg");
    }

    [Fact]
    public void CreateDefaults_PublicCdn_DomainsIncludeAllHostnames()
    {
        var cdn = BuiltinDomainGroups.CreateDefaults().First(g => g.Id == "public-cdn");
        cdn.Domains.Should().Contain(PublicCdnHostnames);
    }

    [Fact]
    public void CreateDefaults_AllGroupsDisabledAndNotFavorite()
    {
        var groups = BuiltinDomainGroups.CreateDefaults();
        groups.Should().OnlyContain(g => !g.Enabled);
        groups.Should().OnlyContain(g => !g.IsFavorite);

        var cdn = groups.First(g => g.Id == "public-cdn");
        cdn.SubItems.Where(s => s.Id is "cdn-jsdelivr" or "cdn-cdnjs" or "cdn-unpkg")
            .Should().OnlyContain(s => !s.Enabled);
    }
}
