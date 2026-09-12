using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;
using Xunit;

namespace UniversalDeviceToolkit.Tests.PackageDownloader;

[Trait("Category", TestCategories.Unit)]
public sealed class BiosPackageRuleTests
{
    [Fact]
    public void TryCreate_WithMalformedLevel_ShouldStillCreateRule()
    {
        var document = new XmlDocument();
        document.LoadXml("""
                         <_Bios>
                           <Level>bad</Level>
                         </_Bios>
                         """);

        var created = BiosPackageRule.TryCreate(document.DocumentElement, out var rule);

        created.Should().BeTrue();
    }

    [Fact]
    public async Task CheckDependenciesSatisfiedAsync_WithMalformedLevel_ShouldSkipMalformedLevel()
    {
        var document = new XmlDocument();
        document.LoadXml("""
                         <_Bios>
                           <Level>bad</Level>
                         </_Bios>
                         """);
        BiosPackageRule.TryCreate(document.DocumentElement, out var rule);

        var satisfied = await rule.CheckDependenciesSatisfiedAsync([], null!, CancellationToken.None);

        // Note: BeFalse here could mean either "malformed level was skipped"
        // or "no BIOS version is available on this machine". The test cannot
        // distinguish between the two without mocking BiosVersion.
        satisfied.Should().BeFalse();
    }
}
