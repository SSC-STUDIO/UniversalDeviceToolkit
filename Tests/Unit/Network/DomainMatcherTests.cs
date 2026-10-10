using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class DomainMatcherTests
{
    [Theory]
    [InlineData("github.com", "github.com", true)]
    [InlineData("api.github.com", "github.com", true)]
    [InlineData("notgithub.com", "github.com", false)]
    [InlineData("steamcommunity.com", "steampowered.com", false)]
    [InlineData("cdn.steamstatic.com", "steamstatic.com", true)]
    public void Matches_HostAgainstRule(string host, string rule, bool expected)
    {
        DomainMatcher.Matches(host, rule).Should().Be(expected);
    }

    [Fact]
    public void MatchesAny_WhenOneRuleMatches()
    {
        DomainMatcher.MatchesAny("api.github.com", ["steamcommunity.com", "github.com"]).Should().BeTrue();
        DomainMatcher.MatchesAny("example.com", ["github.com", "steamcommunity.com"]).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_EmptyOrNullAllowlist_DeniesAll()
    {
        // Fail closed: empty/null allowlist must not open the loopback proxy to all hosts.
        DomainMatcher.IsAllowed("evil.example", null).Should().BeFalse();
        DomainMatcher.IsAllowed("evil.example", Array.Empty<string>()).Should().BeFalse();
        DomainMatcher.IsAllowed("evil.example", []).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_NonEmptyAllowlist_EnforcesSuffixMatch()
    {
        string[] rules = ["github.com", "steamcommunity.com"];
        DomainMatcher.IsAllowed("api.github.com", rules).Should().BeTrue();
        DomainMatcher.IsAllowed("github.com", rules).Should().BeTrue();
        DomainMatcher.IsAllowed("notgithub.com", rules).Should().BeFalse();
        DomainMatcher.IsAllowed("example.com", rules).Should().BeFalse();
        DomainMatcher.IsAllowed(null, rules).Should().BeFalse();
        DomainMatcher.IsAllowed("  ", rules).Should().BeFalse();
    }
}
