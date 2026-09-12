using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class NetworkAccelerationConfigDefaultsTests
{
    [Fact]
    public void CreateDefault_AccelerationIsOff()
    {
        var config = NetworkAccelerationConfig.CreateDefault();
        config.AccelerationEnabled.Should().BeFalse();
        config.Mode.Should().Be(NetworkAccelerationMode.Off);
        config.ListenPort.Should().Be(NetworkAccelerationDefaults.DefaultListenPort);
        config.ShowInNavigation.Should().BeTrue();
    }

    [Fact]
    public void CreateDefault_IncludesBuiltinDomainGroupsDisabled()
    {
        var config = NetworkAccelerationConfig.CreateDefault();
        config.DomainGroups.Should().NotBeEmpty();
        config.DomainGroups.Should().Contain(g => g.Id == "steam");
        config.DomainGroups.Should().Contain(g => g.Id == "github");
        config.DomainGroups.Should().OnlyContain(g => !g.Enabled);
        config.DomainGroups.Should().OnlyContain(g => !g.IsFavorite);
        config.DomainGroups.First(g => g.Id == "steam").Domains.Should().Contain("steamcommunity.com");
        config.DomainGroups.First(g => g.Id == "github").Domains.Should().Contain("github.com");
    }

    [Fact]
    public void NetworkDomainGroup_IsFavorite_DefaultsFalseAndIsSettable()
    {
        var group = new NetworkDomainGroup { Id = "steam", DisplayName = "Steam" };
        group.IsFavorite.Should().BeFalse();
        group.IsFavorite = true;
        group.IsFavorite.Should().BeTrue();
    }
}
