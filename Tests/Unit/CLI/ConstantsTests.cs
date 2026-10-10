using FluentAssertions;
using UniversalDeviceToolkit.CLI.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Trait("Category", TestCategories.Unit)]
public class ConstantsTests
{
    [Fact]
    public void DefaultPipeName_IsStable()
    {
        Constants.DEFAULT_PIPE_NAME.Should().Be("LenovoLegionToolkit-IPC-0");
    }

    [Fact]
    public void PreferredPipeName_IsUdtBranded()
    {
        Constants.PREFERRED_PIPE_NAME.Should().Be("UniversalDeviceToolkit-IPC-0");
    }

    [Fact]
    public void GetServerPipeNames_OrdersLegacyPrimaryThenPreferred()
    {
        Constants.GetServerPipeNames().Should().Equal(
            Constants.DEFAULT_PIPE_NAME,
            Constants.PREFERRED_PIPE_NAME);
    }

    [Fact]
    public void GetClientPipeNames_OrdersPreferredThenLegacyFallback()
    {
        Constants.GetClientPipeNames().Should().Equal(
            Constants.PREFERRED_PIPE_NAME,
            Constants.DEFAULT_PIPE_NAME);
    }

    [Fact]
    public void GetPipeName_WithPreferredBase_ShouldReturnPreferredWithoutIsolation()
    {
        Constants.GetPipeName(null, Constants.PREFERRED_PIPE_NAME)
            .Should().Be(Constants.PREFERRED_PIPE_NAME);
    }
}
