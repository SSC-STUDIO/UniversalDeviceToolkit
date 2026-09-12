using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class PacFileGeneratorTests
{
    [Fact]
    public void Generate_WithoutDomains_ReturnsDirectOnly()
    {
        var pac = PacFileGenerator.Generate(34123);
        pac.Should().Contain("FindProxyForURL");
        pac.Should().Contain("return \"DIRECT\"");
        pac.Should().NotContain("PROXY 127.0.0.1");
    }

    [Fact]
    public void Generate_WithDomains_IncludesLoopbackProxy()
    {
        var pac = PacFileGenerator.Generate(34123, ["steamcommunity.com"]);
        pac.Should().Contain("steamcommunity.com");
        pac.Should().Contain("PROXY 127.0.0.1:34123");
        pac.Should().Contain("PROXY [::1]:34123");
    }
}
