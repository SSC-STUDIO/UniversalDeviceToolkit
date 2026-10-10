using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class NetworkAccelerationStartSafetyTests
{
    [Fact]
    public void CanApplySystemProxy_WhenNullOrEmpty_IsFalse()
    {
        NetworkAccelerationService.CanApplySystemProxy(null).Should().BeFalse();
        NetworkAccelerationService.CanApplySystemProxy([]).Should().BeFalse();
        NetworkAccelerationService.CanApplySystemProxy(["", "  "]).Should().BeFalse();
    }

    [Fact]
    public void CanApplySystemProxy_WhenAnyNonEmptyDomain_IsTrue()
    {
        NetworkAccelerationService.CanApplySystemProxy(["steamcommunity.com"]).Should().BeTrue();
        NetworkAccelerationService.CanApplySystemProxy(["", "github.com"]).Should().BeTrue();
    }

    [Fact]
    public void CanStartMode_Hosts_IsAlwaysRefused()
    {
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.Hosts, null).Should().BeFalse();
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.Hosts, []).Should().BeFalse();
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.Hosts, ["example.com"]).Should().BeFalse();
    }

    [Fact]
    public void CanStartMode_SystemProxy_RequiresEnabledDomains()
    {
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.SystemProxy, null).Should().BeFalse();
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.SystemProxy, []).Should().BeFalse();
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.SystemProxy, ["steamcommunity.com"]).Should().BeTrue();
    }

    [Fact]
    public void CanStartMode_DiagnosticsOnly_IsAlwaysAllowed()
    {
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.DiagnosticsOnly, null).Should().BeTrue();
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.DiagnosticsOnly, []).Should().BeTrue();
    }

    [Fact]
    public void CanStartMode_Off_IsRefused()
    {
        NetworkAccelerationService.CanStartMode(NetworkAccelerationMode.Off, ["x.com"]).Should().BeFalse();
    }

    [Fact]
    public void CreateDefault_StillDisabledAndNeverImpliesRunning()
    {
        var config = NetworkAccelerationConfig.CreateDefault();
        config.AccelerationEnabled.Should().BeFalse();
        config.Mode.Should().Be(NetworkAccelerationMode.Off);
        NetworkAccelerationService.CanStartMode(config.Mode, config.DomainGroups.SelectMany(g => g.Domains)).Should().BeFalse();
    }
}
