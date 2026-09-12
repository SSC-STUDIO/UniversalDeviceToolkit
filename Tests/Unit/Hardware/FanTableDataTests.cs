using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class FanTableDataTests
{
    [Fact]
    public void Properties_ShouldReturnConstructorValues()
    {
        ushort[] fanSpeeds = [1000, 2000, 3000];
        ushort[] temps = [40, 50, 60];
        var data = new FanTableData(FanTableType.CPU, 1, 2, fanSpeeds, temps);
        data.Type.Should().Be(FanTableType.CPU);
        data.FanId.Should().Be(1);
        data.SensorId.Should().Be(2);
        data.FanSpeeds.Should().ContainInOrder(1000, 2000, 3000);
        data.Temps.Should().ContainInOrder(40, 50, 60);
    }

    [Fact]
    public void ToString_ShouldContainTypeName()
    {
        ushort[] speeds = [500];
        ushort[] temps = [30];
        var data = new FanTableData(FanTableType.GPU, 0, 0, speeds, temps);
        data.ToString().Should().Contain("GPU");
        data.ToString().Should().Contain("FanId");
    }

    [Fact]
    public void Constructor_WithEmptyArrays_ShouldWork()
    {
        var data = new FanTableData(FanTableType.Unknown, 0, 0, [], []);
        data.FanSpeeds.Should().BeEmpty();
        data.Temps.Should().BeEmpty();
    }

    [Fact]
    public void AllFanTableTypes_ShouldBeUsable()
    {
        foreach (var type in Enum.GetValues<FanTableType>())
        {
            var data = new FanTableData(type, 0, 0, [100], [25]);
            data.Type.Should().Be(type);
        }
    }
}