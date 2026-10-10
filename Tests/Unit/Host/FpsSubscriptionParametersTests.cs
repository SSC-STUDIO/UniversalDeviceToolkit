using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class FpsSubscriptionParametersTests
{
    [Fact]
    public void MissingSubscriberId_PreservesLegacyReferenceCounting()
    {
        Assert.True(FpsSubscriptionManager.TryReadSubscriberId(default, out var subscriberId));
        Assert.Null(subscriberId);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"blacklist\":[\"game.exe\"]}")]
    public void LegacyParameters_LeaveSubscriberIdAbsent(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.True(FpsSubscriptionManager.TryReadSubscriberId(document.RootElement, out var subscriberId));
        Assert.Null(subscriberId);
    }

    [Theory]
    [InlineData("{\"subscriberId\":null}")]
    [InlineData("{\"subscriberId\":5}")]
    [InlineData("{\"subscriberId\":true}")]
    [InlineData("{\"subscriberId\":[]}")]
    [InlineData("{\"subscriberId\":{}}")]
    [InlineData("{\"subscriberId\":\"\"}")]
    [InlineData("{\"subscriberId\":\" \"}")]
    [InlineData("{\"subscriberId\":\" osd\"}")]
    [InlineData("""{"subscriberId":"osd "}""")]
    [InlineData("{\"subscriberId\":\"osd\\tother\"}")]
    [InlineData("{\"subscriberId\":\"osd\\u0000other\"}")]
    public void ExplicitInvalidSubscriberId_IsNotSilentlyTreatedAsLegacy(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.False(FpsSubscriptionManager.TryReadSubscriberId(document.RootElement, out var subscriberId));
        Assert.Null(subscriberId);
    }

    [Theory]
    [InlineData("webview2-osd")]
    [InlineData("dashboard")]
    public void NamedSubscriberId_CanBeCombinedWithTheLegacyBlacklist(string expected)
    {
        var parameters = JsonSerializer.SerializeToElement(new { subscriberId = expected, blacklist = new[] { "game.exe" } });
        Assert.True(FpsSubscriptionManager.TryReadSubscriberId(parameters, out var subscriberId));
        Assert.Equal(expected, subscriberId);
        Assert.Equal(new[] { "game.exe" }, SensorsHandlers.ParseFpsBlacklist(parameters));
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void SubscriberIdLength_IsBounded(int length, bool valid)
    {
        var parameters = JsonSerializer.SerializeToElement(new { subscriberId = new string('a', length) });
        Assert.Equal(valid, FpsSubscriptionManager.TryReadSubscriberId(parameters, out _));
    }

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
