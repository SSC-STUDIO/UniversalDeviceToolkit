using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class FpsSubscriptionParametersTests
{
    [Fact]
    public void MissingParameters_LeaveTheBlacklistUnchanged()
    {
        Assert.Null(SensorsHandlers.ParseFpsBlacklist(default));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("\"ignored\"")]
    [InlineData("{}")]
    [InlineData("{\"blacklist\":null}")]
    [InlineData("{\"blacklist\":\"ignored\"}")]
    [InlineData("{\"blacklist\":[]}")]
    public void OptionalParameters_DoNotRejectFpsSubscriptions(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Null(SensorsHandlers.ParseFpsBlacklist(document.RootElement));
    }

    [Fact]
    public void Blacklist_TrimsStringsAndRemovesDuplicatesAndInvalidEntries()
    {
        using var document = JsonDocument.Parse("""{"blacklist":[" Game.exe ","game.EXE",null,1,"", " ","other.exe"]}""");
        Assert.Equal(new[] { "Game.exe", "other.exe" }, SensorsHandlers.ParseFpsBlacklist(document.RootElement));
    }
}
