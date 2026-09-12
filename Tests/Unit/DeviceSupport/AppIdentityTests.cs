using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class FeatureStateMessageTests
{
    [Fact]
    public void State_ShouldRetainValue()
    {
        var msg = new FeatureStateMessage<bool>(true);
        msg.State.Should().BeTrue();
    }

    [Fact]
    public void State_ShouldRetainFalse()
    {
        var msg = new FeatureStateMessage<bool>(false);
        msg.State.Should().BeFalse();
    }

    [Fact]
    public void State_Int_ShouldRetainValue()
    {
        var msg = new FeatureStateMessage<int>(42);
        msg.State.Should().Be(42);
    }

    [Fact]
    public void State_String_ShouldRetainValue()
    {
        var msg = new FeatureStateMessage<string>("hello");
        msg.State.Should().Be("hello");
    }

    [Fact]
    public void State_NullableInt_ShouldRetainNull()
    {
        var msg = new FeatureStateMessage<int?>(null);
        msg.State.Should().BeNull();
    }

    [Fact]
    public void ImplementsIMessage()
    {
        var msg = new FeatureStateMessage<bool>(true);
        msg.Should().BeAssignableTo<IMessage>();
    }
}

[Trait("Category", TestCategories.Unit)]
public class AppIdentityTests
{
    [Fact]
    public void DisplayName_ShouldBeUniversalDeviceToolkit()
    {
        AppIdentity.DisplayName.Should().Be("Universal Device Toolkit");
    }

    [Fact]
    public void CompactName_ShouldMatch()
    {
        AppIdentity.CompactName.Should().Be("UniversalDeviceToolkit");
    }

    [Fact]
    public void LegacyDisplayName_ShouldBeUniversalDeviceToolkit()
    {
        AppIdentity.LegacyDisplayName.Should().Be("Lenovo Legion Toolkit");
    }

    [Fact]
    public void LegacyCompactName_ShouldMatch()
    {
        AppIdentity.LegacyCompactName.Should().Be("LenovoLegionToolkit");
    }

    [Fact]
    public void Publisher_ShouldBeChenRunsen()
    {
        AppIdentity.Publisher.Should().Be("ChenRunsen");
    }

    [Fact]
    public void RepositoryOwner_ShouldBeSSC_STUDIO()
    {
        AppIdentity.RepositoryOwner.Should().Be("SSC-STUDIO");
    }

    [Fact]
    public void RepositoryName_ShouldBeUniversalDeviceToolkit()
    {
        AppIdentity.RepositoryName.Should().Be("UniversalDeviceToolkit");
    }

    [Fact]
    public void LegacyRepositoryName_ShouldBeUniversalDeviceToolkit()
    {
        AppIdentity.LegacyRepositoryName.Should().Be("LenovoLegionToolkit");
    }

    [Fact]
    public void RepositoryUrl_ShouldBeGitHubUrl()
    {
        AppIdentity.RepositoryUrl.Should().StartWith("https://github.com/");
        AppIdentity.RepositoryUrl.Should().Contain("UniversalDeviceToolkit");
    }

    [Fact]
    public void LegacyRepositoryUrl_ShouldBeGitHubUrl()
    {
        AppIdentity.LegacyRepositoryUrl.Should().StartWith("https://github.com/");
        AppIdentity.LegacyRepositoryUrl.Should().Contain("LenovoLegionToolkit");
    }

    [Fact]
    public void ResourcesBaseUrl_ShouldStartWithHttps()
    {
        AppIdentity.ResourcesBaseUrl.Should().StartWith("https://");
    }

    [Fact]
    public void StableResourceCatalogUrl_ShouldEndWithCatalogJson()
    {
        AppIdentity.StableResourceCatalogUrl.Should().EndWith("/catalog.json");
    }

    [Fact]
    public void StableResourceCatalogUrl_ShouldContainBaseUrl()
    {
        AppIdentity.StableResourceCatalogUrl.Should().StartWith(AppIdentity.ResourcesBaseUrl);
    }
}