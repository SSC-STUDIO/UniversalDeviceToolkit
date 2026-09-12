using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.System.Management;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Collection("FeatureHandlers")]
[Trait("Category", TestCategories.Unit)]
public sealed class FeatureHandlersTests : UnitTestBase
{
    public FeatureHandlersTests()
    {
        FeatureHandlers.ResetFeaturesForTests();
    }

    public override void Dispose()
    {
        FeatureHandlers.ResetFeaturesForTests();
        base.Dispose();
    }

    [Fact]
    public async Task SetState_CamelCaseDpiScale_AppliesParsedScale()
    {
        DpiScale? applied = null;
        var feature = new Mock<IFeature<DpiScale>>();
        feature
            .Setup(f => f.SetStateAsync(It.IsAny<DpiScale>(), It.IsAny<CancellationToken>()))
            .Callback<DpiScale, CancellationToken>((state, _) => applied = state)
            .Returns(Task.CompletedTask);

        FeatureHandlers.RegisterFeatureForTests("dpiScale", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(
            Request("""{"feature":"dpiScale","state":{"scale":125}}"""));

        result.IsError.Should().BeFalse();
        applied.Should().NotBeNull();
        applied!.Value.Scale.Should().Be(125);
        feature.Verify(f => f.InvalidateResolution(), Times.Once);
    }

    [Fact]
    public async Task SetState_UndefinedNumericEnum_ReturnsUndefinedState()
    {
        var feature = new Mock<IFeature<PowerModeState>>();
        FeatureHandlers.RegisterFeatureForTests("powerMode", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(
            Request("""{"feature":"powerMode","state":"99"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.UndefinedState);
        result.ErrorMessage.Should().Be("UNDEFINED_STATE");
        feature.Verify(
            f => f.SetStateAsync(It.IsAny<PowerModeState>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetState_UnknownEnumName_ReturnsUndefinedState()
    {
        var feature = new Mock<IFeature<PowerModeState>>();
        FeatureHandlers.RegisterFeatureForTests("powerMode", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(
            Request("""{"feature":"powerMode","state":"NotAMode"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.UndefinedState);
    }

    [Fact]
    public async Task SetState_PowerModeWithoutAc_ReturnsAcRequired()
    {
        var feature = new Mock<IFeature<PowerModeState>>();
        feature
            .Setup(f => f.SetStateAsync(PowerModeState.Performance, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PowerModeUnavailableWithoutACException(PowerModeState.Performance));
        FeatureHandlers.RegisterFeatureForTests("powerMode", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(
            Request("""{"feature":"powerMode","state":"Performance"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.AcPowerRequired);
        result.ErrorMessage.Should().StartWith("AC_REQUIRED");
    }

    [Fact]
    public async Task SetState_IgpuModeChangeException_IsErrorNotSuccess()
    {
        var feature = new Mock<IFeature<IGPUModeState>>();
        feature
            .Setup(f => f.SetStateAsync(IGPUModeState.IGPUOnly, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IGPUModeChangeException(IGPUModeState.IGPUOnly));
        FeatureHandlers.RegisterFeatureForTests("igpuMode", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(
            Request("""{"feature":"igpuMode","state":"IGPUOnly"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InternalError);
        result.ErrorMessage.Should().Contain(nameof(IGPUModeChangeException));
    }

    [Fact]
    public async Task SetState_WmiWriteTimeout_IsErrorNotSuccess()
    {
        var feature = new Mock<IFeature<BatteryState>>();
        feature
            .Setup(f => f.SetStateAsync(BatteryState.Conservation, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WmiWriteIndeterminateException("root\\WMI", "SELECT * FROM LENOVO_OTHER_METHOD", "SetFeatureValue", 3000));
        FeatureHandlers.RegisterFeatureForTests("battery", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(
            Request("""{"feature":"battery","state":"Conservation"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InternalError);
        result.ErrorMessage.Should().Contain(nameof(WmiWriteIndeterminateException));
        result.ErrorMessage.Should().Contain("may still complete");
    }

    [Fact]
    public async Task GetStates_NotSupported_ReturnsFeatureNotSupported()
    {
        var feature = new Mock<IFeature<BatteryState>>();
        feature
            .Setup(f => f.GetAllStatesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException());
        FeatureHandlers.RegisterFeatureForTests("battery", feature.Object);

        var result = await FeatureHandlers.HandleGetStatesAsync(Request("""{"feature":"battery"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.FeatureNotSupported);
        result.ErrorMessage.Should().Be("NOT_SUPPORTED");
    }

    [Fact]
    public async Task SetState_MissingState_ReturnsInvalidParams()
    {
        var feature = new Mock<IFeature<BatteryState>>();
        FeatureHandlers.RegisterFeatureForTests("battery", feature.Object);

        var result = await FeatureHandlers.HandleSetStateAsync(Request("""{"feature":"battery"}"""));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InvalidParams);
    }

    [Fact]
    public async Task GetFeature_UnknownKey_ReturnsInvalidParams()
    {
        var act = () => FeatureHandlers.GetFeature(Request("""{"feature":"notAFeature"}"""));

        act.Should().Throw<BridgeErrorException>()
            .Which.Code.Should().Be(BridgeErrorCodes.InvalidParams);
    }

    [Fact]
    public void MapSetStateException_WmiBusy_IsNotSuccess()
    {
        var result = FeatureHandlers.MapSetStateException(
            new WmiWriteBusyException("root\\WMI", "SELECT * FROM LENOVO_OTHER_METHOD", "SetFeatureValue", 3000));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InternalError);
        result.ErrorMessage.Should().Contain(nameof(WmiWriteBusyException));
    }

    private static BridgeRequest Request(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new BridgeRequest(1, "feature.test", document.RootElement.Clone());
    }
}

[CollectionDefinition("FeatureHandlers", DisableParallelization = true)]
public sealed class FeatureHandlersCollection;
