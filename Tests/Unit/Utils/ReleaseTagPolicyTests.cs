using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public sealed class ReleaseTagPolicyTests
{
    [Fact]
    public void IsCatalogTag_RecognizesStableAndPreview()
    {
        ReleaseTagPolicy.IsCatalogTag("plugin-catalog").Should().BeTrue();
        ReleaseTagPolicy.IsCatalogTag("plugin-catalog-preview").Should().BeTrue();
        ReleaseTagPolicy.IsCatalogTag("v6.0.0-preview.1").Should().BeFalse();
        ReleaseTagPolicy.IsCatalogTag("latest").Should().BeFalse();
        ReleaseTagPolicy.IsCatalogTag(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("6.0.0", false)]
    [InlineData("6.0.0+abc123", false)]
    [InlineData("v6.0.0", false)]
    [InlineData("6.0.0-preview.1", true)]
    [InlineData("v6.0.0-preview.1", true)]
    [InlineData("6.0.0-preview.1+deadbeef", true)]
    public void IsPrereleaseApplicationVersion_MatchesSemVerHyphenLikeReleaseWorkflow(
        string informationalVersion,
        bool expected)
    {
        ReleaseTagPolicy.IsPrereleaseApplicationVersion(informationalVersion).Should().Be(expected);
    }

    [Theory]
    [InlineData("v1.2.3", false, false, false, true)]
    [InlineData("v1.2.3", true, false, false, false)]
    [InlineData("plugin-catalog", false, false, false, false)]
    [InlineData("v6.0.0-preview.1", false, false, false, false)]
    [InlineData("v6.0.0-preview.1", false, false, true, true)]
    public void IsPublicApplicationRelease_FiltersReleasesForUpdateChecks(
        string tagName,
        bool draft,
        bool prereleaseFlag,
        bool includePrerelease,
        bool expected)
    {
        ReleaseTagPolicy.IsPublicApplicationRelease(tagName, draft, prereleaseFlag, includePrerelease)
            .Should().Be(expected);
    }
}
