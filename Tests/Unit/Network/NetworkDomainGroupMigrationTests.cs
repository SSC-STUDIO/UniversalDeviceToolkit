using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class NetworkDomainGroupMigrationTests
{
    [Fact]
    public void MigrateDomainGroups_RemovesCustomGroup()
    {
        var groups = new List<NetworkDomainGroup>
        {
            new() { Id = "steam", DisplayName = "Steam" },
            new() { Id = "Custom", DisplayName = "Custom" },
            BuiltinDomainGroups.CreateDefaults().First(g => g.Id == "public-cdn")
        };

        NetworkAccelerationService.MigrateDomainGroups(groups).Should().BeTrue();
        groups.Should().NotContain(g => string.Equals(g.Id, "custom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MigrateDomainGroups_AddsAllBuiltinGroupsWhenMissing()
    {
        var groups = new List<NetworkDomainGroup>
        {
            new() { Id = "steam", DisplayName = "Steam" }
        };

        NetworkAccelerationService.MigrateDomainGroups(groups).Should().BeTrue();

        var defaults = BuiltinDomainGroups.CreateDefaults();
        groups.Select(g => g.Id).Should().Contain(defaults.Select(g => g.Id));
        groups.Where(g => defaults.Any(d => d.Id == g.Id)).Should().OnlyContain(g => !g.Enabled);
        groups.First(g => g.Id == "public-cdn").SubItems.Should().HaveCount(9);
    }

    [Fact]
    public void MigrateDomainGroups_MergesMissingSubItemsAndDomains_PreservingUserEnabledState()
    {
        var userCdn = new NetworkDomainGroup
        {
            Id = "public-cdn",
            DisplayName = "Public CDN",
            Enabled = true,
            Domains = ["translate.googleapis.com", "user-added.example.com"],
            SubItems =
            [
                new() { Id = "cdn-fonts-gstatic", DisplayName = "fonts.gstatic.com", Domain = "fonts.gstatic.com", Enabled = true },
            ]
        };
        var groups = new List<NetworkDomainGroup> { userCdn };

        NetworkAccelerationService.MigrateDomainGroups(groups).Should().BeTrue();

        // User's group instance, Enabled flags and custom domain entries survive the merge.
        groups.Should().Contain(userCdn);
        userCdn.Enabled.Should().BeTrue();
        userCdn.SubItems.First(s => s.Id == "cdn-fonts-gstatic").Enabled.Should().BeTrue();
        userCdn.SubItems.Should().Contain(s => s.Id == "cdn-jsdelivr");
        userCdn.SubItems.Should().Contain(s => s.Id == "cdn-cdnjs");
        userCdn.SubItems.Should().Contain(s => s.Id == "cdn-unpkg");
        userCdn.Domains.Should().Contain("user-added.example.com");
        userCdn.Domains.Should().Contain("unpkg.com");
        userCdn.Domains.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void MigrateDomainGroups_RepairsSteamAndGithubSubItemsWithoutOverwritingUserFields()
    {
        var steam = new NetworkDomainGroup
        {
            Id = "steam",
            DisplayName = "My Steam",
            IconKey = "custom-icon",
            Enabled = true,
            IsFavorite = true,
            Domains = ["user-steam.example"],
            SubItems = [new() { Id = "steam-store", Enabled = true }]
        };
        var github = new NetworkDomainGroup { Id = "github", Enabled = true };
        var groups = new List<NetworkDomainGroup> { steam, github };

        NetworkAccelerationService.MigrateDomainGroups(groups).Should().BeTrue();

        groups.First(g => g.Id == "steam").Should().BeSameAs(steam);
        steam.DisplayName.Should().Be("My Steam");
        steam.IconKey.Should().Be("custom-icon");
        steam.Enabled.Should().BeTrue();
        steam.IsFavorite.Should().BeTrue();
        steam.Domains.Should().Contain("user-steam.example");
        steam.SubItems.Should().Contain(s => s.Id == "steam-store" && s.Enabled);
        steam.SubItems.Should().Contain(s => s.Id == "steam-images");

        github.SubItems.Should().NotBeEmpty();
        github.Domains.Should().Contain("github.com");
        groups.Should().Contain(g => g.Id == "twitch" && !g.Enabled);
    }

    [Fact]
    public void CollectEnabledDomains_IncludesEnabledSubItemsAndExcludesDisabledSubItems()
    {
        var groups = new List<NetworkDomainGroup>
        {
            new()
            {
                Id = "steam",
                Enabled = true,
                Domains = ["SteamPowered.COM"],
                SubItems =
                [
                    new() { Id = "enabled", Domain = "cdn.example.com", Enabled = true },
                    new() { Id = "disabled", Domain = "disabled.example.com", Enabled = false }
                ]
            },
            new() { Id = "github", Enabled = false, Domains = ["github.com"] }
        };

        var domains = NetworkAccelerationService.CollectEnabledDomains(groups);

        domains.Should().Contain("steampowered.com");
        domains.Should().Contain("cdn.example.com");
        domains.Should().NotContain("disabled.example.com");
        domains.Should().NotContain("github.com");
    }

    [Fact]
    public void MigrateDomainGroups_WhenAlreadyMigrated_ReturnsFalse()
    {
        var groups = BuiltinDomainGroups.CreateDefaults();
        NetworkAccelerationService.MigrateDomainGroups(groups).Should().BeFalse();
    }
}
