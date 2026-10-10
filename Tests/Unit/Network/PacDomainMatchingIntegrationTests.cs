using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class PacDomainMatchingIntegrationTests
{
    [Fact]
    public void Generate_WithSteamDomains_MatchesExpectedHostsInPac()
    {
        var steam = BuiltinDomainGroups.CreateDefaults().First(g => g.Id == "steam");
        var pac = PacFileGenerator.Generate(34123, steam.Domains);
        pac.Should().Contain("steamcommunity.com");
        DomainMatcher.Matches("store.steampowered.com", "steampowered.com").Should().BeTrue();
    }
}
