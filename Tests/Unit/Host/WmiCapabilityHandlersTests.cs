using FluentAssertions;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Lib.System.Management;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class WmiCapabilityHandlersTests : UnitTestBase
{
    [Fact]
    public void MapSetGodModeFnQException_WmiTimeout_IsErrorNotSuccess()
    {
        var result = WmiCapabilityHandlers.MapSetGodModeFnQException(
            new WmiWriteIndeterminateException("root\\WMI", "SELECT * FROM LENOVO_OTHER_METHOD", "SetFeatureValue", 3000));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InternalError);
        result.ErrorMessage.Should().Contain(nameof(WmiWriteIndeterminateException));
        result.ErrorMessage.Should().Contain("may still complete");
    }

    [Fact]
    public void MapSetGodModeFnQException_WmiBusy_IsErrorNotSuccess()
    {
        var result = WmiCapabilityHandlers.MapSetGodModeFnQException(
            new WmiWriteBusyException("root\\WMI", "SELECT * FROM LENOVO_OTHER_METHOD", "SetFeatureValue", 3000));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InternalError);
        result.ErrorMessage.Should().Contain("was not launched");
    }

    [Fact]
    public void MapSetGodModeFnQException_InvalidParams_PreservesCode()
    {
        var result = WmiCapabilityHandlers.MapSetGodModeFnQException(
            new BridgeErrorException(-32602, "Missing boolean parameter 'enabled'."));

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.InvalidParams);
    }
}
