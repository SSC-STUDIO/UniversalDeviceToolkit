using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class HostsMarkedBlockTests
{
    [Fact]
    public void TryExtract_WhenMarkersPresent_ReturnsBody()
    {
        var hosts = """
            127.0.0.1 localhost

            # BEGIN UDT-NETWORK-ACCELERATION
            127.0.0.1 example.test
            # END UDT-NETWORK-ACCELERATION

            # other
            """;

        HostsMarkedBlock.TryExtract(hosts, out var body).Should().BeTrue();
        body.Should().Be("127.0.0.1 example.test");
    }

    [Fact]
    public void Upsert_WhenMissing_AppendsBlock()
    {
        var result = HostsMarkedBlock.Upsert("127.0.0.1 localhost", ["127.0.0.1 a.test"]);
        result.Should().Contain(HostsMarkedBlock.BeginMarker);
        result.Should().Contain("127.0.0.1 a.test");
        result.Should().Contain(HostsMarkedBlock.EndMarker);
        result.Should().Contain("127.0.0.1 localhost");
    }

    [Fact]
    public void Upsert_WhenPresent_ReplacesOnlyMarkedBlock()
    {
        var original = """
            keep-me
            # BEGIN UDT-NETWORK-ACCELERATION
            127.0.0.1 old.test
            # END UDT-NETWORK-ACCELERATION
            tail
            """;

        var result = HostsMarkedBlock.Upsert(original, ["127.0.0.1 new.test"]);
        result.Should().Contain("keep-me");
        result.Should().Contain("tail");
        result.Should().Contain("127.0.0.1 new.test");
        result.Should().NotContain("old.test");
    }

    [Fact]
    public void Remove_WhenPresent_RemovesOnlyMarkedBlock()
    {
        var original = """
            keep
            # BEGIN UDT-NETWORK-ACCELERATION
            127.0.0.1 x
            # END UDT-NETWORK-ACCELERATION
            """;

        var result = HostsMarkedBlock.Remove(original);
        result.Should().Contain("keep");
        result.Should().NotContain(HostsMarkedBlock.BeginMarker);
        result.Should().NotContain("127.0.0.1 x");
    }
}
