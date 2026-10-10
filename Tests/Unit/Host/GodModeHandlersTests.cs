using System;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Lib.System.Management;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class GodModeHandlersTests : UnitTestBase
{
    [Fact]
    public void ParseState_MissingActivePresetId_ThrowsInvalidParams()
    {
        using var document = JsonDocument.Parse("""{"Presets":{}}""");

        var act = () => GodModeHandlers.ParseState(document.RootElement);

        act.Should().Throw<BridgeErrorException>()
            .Which.Code.Should().Be(BridgeErrorCodes.InvalidParams);
    }

    [Fact]
    public void ParseState_InvalidPresetKey_ThrowsInvalidParams()
    {
        using var document = JsonDocument.Parse(
            """{"ActivePresetId":"11111111-1111-1111-1111-111111111111","Presets":{"not-a-guid":{"Name":"A"}}}""");

        var act = () => GodModeHandlers.ParseState(document.RootElement);

        act.Should().Throw<BridgeErrorException>()
            .Which.Message.Should().Contain("not a valid GUID");
    }

    [Fact]
    public void ParseState_InvalidFanTableLength_ThrowsInvalidParams()
    {
        var presetId = "11111111-1111-1111-1111-111111111111";
        using var document = JsonDocument.Parse(
            "{\"ActivePresetId\":\"" + presetId + "\",\"Presets\":{\"" + presetId + "\":{\"Name\":\"A\",\"FanTable\":[1,2,3]}}}");

        var act = () => GodModeHandlers.ParseState(document.RootElement);

        act.Should().Throw<BridgeErrorException>()
            .Which.Message.Should().Contain("FanTable");
    }

    [Fact]
    public void ParseState_OutOfRangeFanSpeed_ThrowsInvalidParams()
    {
        var presetId = "11111111-1111-1111-1111-111111111111";
        using var document = JsonDocument.Parse(
            "{\"ActivePresetId\":\"" + presetId + "\",\"Presets\":{\"" + presetId + "\":{\"Name\":\"A\",\"FanTable\":[1,2,3,4,5,6,7,8,9,99]}}}");

        var act = () => GodModeHandlers.ParseState(document.RootElement);

        act.Should().Throw<BridgeErrorException>()
            .Which.Message.Should().Contain("FanTable");
    }

    [Fact]
    public void ParseState_ValidCamelAndPascalPayload_KeepsActivePreset()
    {
        var presetId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var document = JsonDocument.Parse(
            "{\"activePresetId\":\"" + presetId + "\",\"presets\":{\"" + presetId + "\":{\"name\":\"Quiet\",\"fanTable\":[1,2,3,4,5,6,7,8,9,10]}}}");

        var state = GodModeHandlers.ParseState(document.RootElement);

        state.ActivePresetId.Should().Be(presetId);
        state.Presets.Should().ContainKey(presetId);
        state.Presets[presetId].Name.Should().Be("Quiet");
        state.Presets[presetId].FanTableInfo.Should().NotBeNull();
        state.Presets[presetId].FanTableInfo!.Value.Table.GetTable().Should().Equal(
            new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void MapGodModeException_WmiIndeterminate_IsErrorNotSuccess()
    {
        var result = GodModeHandlers.MapGodModeException(
            new WmiWriteIndeterminateException("root\\WMI", "SELECT * FROM LENOVO_OTHER_METHOD", "SetFeatureValue", 3000));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InternalError);
        result.ErrorMessage.Should().Contain(nameof(WmiWriteIndeterminateException));
    }

    [Fact]
    public void MapGodModeException_InvalidParams_PreservesCode()
    {
        var result = GodModeHandlers.MapGodModeException(new BridgeErrorException(-32602, "ActivePresetId is required."));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InvalidParams);
        result.ErrorMessage.Should().Be("ActivePresetId is required.");
    }
}
