using FluentAssertions;
using UniversalDeviceToolkit.CLI.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Trait("Category", TestCategories.Unit)]
public class IpcResponseTests
{
    [Fact]
    public void IpcResponse_CanRepresentSuccess()
    {
        var r = new IpcResponse { Success = true, Message = "ok" };

        r.Success.Should().BeTrue();
        r.Message.Should().Be("ok");
    }

    [Fact]
    public void IpcResponse_CanRepresentFailure()
    {
        var r = new IpcResponse { Success = false, Message = "error" };

        r.Success.Should().BeFalse();
        r.Message.Should().Be("error");
    }
}
