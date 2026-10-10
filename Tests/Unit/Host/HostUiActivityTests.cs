using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Collection("HostUiActivity")]
[Trait("Category", TestCategories.Unit)]
public sealed class HostUiActivityTests : UnitTestBase
{
    public HostUiActivityTests()
    {
        HostUiActivity.ResetForTests();
    }

    public override void Dispose()
    {
        HostUiActivity.ResetForTests();
        base.Dispose();
    }

    [Fact]
    public void SetActive_RaisesChangedOnlyWhenValueChanges()
    {
        var seen = new List<bool>();
        HostUiActivity.Changed += seen.Add;

        HostUiActivity.IsActive.Should().BeTrue();
        HostUiActivity.SetActive(true);
        HostUiActivity.SetActive(false);
        HostUiActivity.SetActive(false);
        HostUiActivity.SetActive(true);

        seen.Should().Equal(false, true);
        HostUiActivity.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetUiActive_UpdatesHostActivityAndReturnsPayload()
    {
        HostUiActivity.IsActive.Should().BeTrue();

        var result = await UiActivityHandlers.HandleSetUiActiveAsync(
            Request("""{"active":false,"pid":0}"""));

        result.IsError.Should().BeFalse();
        HostUiActivity.IsActive.Should().BeFalse();
        result.Value.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result.Value);
        json.Should().Contain("\"active\":false");
        json.Should().Contain("\"ok\":true");
    }

    [Fact]
    public void SetActive_WhenBecomingInactive_ShouldStayInactiveUntilRestored()
    {
        HostUiActivity.SetActive(false);
        HostUiActivity.IsActive.Should().BeFalse();
        HostUiActivity.SetActive(true);
        HostUiActivity.IsActive.Should().BeTrue();
    }

    private static BridgeRequest Request(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new BridgeRequest(1, "app.setUiActive", document.RootElement.Clone());
    }
}

[CollectionDefinition("HostUiActivity", DisableParallelization = true)]
public sealed class HostUiActivityCollection;
