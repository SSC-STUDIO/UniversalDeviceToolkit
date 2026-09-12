using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Trait("Category", TestCategories.Unit)]
public class FanTableInfoTests
{
    [Fact]
    public void Properties_ShouldReturnConstructorValues()
    {
        ushort[] fanSpeeds = [1000, 2000];
        ushort[] temps = [40, 50];
        var data = new FanTableData(FanTableType.CPU, 0, 0, fanSpeeds, temps);
        var table = new FanTable([100, 200, 300, 400, 500, 600, 700, 800, 900, 1000]);
        var info = new FanTableInfo([data], table);

        info.Data.Should().HaveCount(1);
        info.Data[0].Type.Should().Be(FanTableType.CPU);
        info.Table.FSS0.Should().Be(100);
    }

    [Fact]
    public void ToString_ShouldContainDataAndTable()
    {
        var table = new FanTable([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        var info = new FanTableInfo([], table);
        var str = info.ToString();
        str.Should().Contain("Data");
        str.Should().Contain("Table");
    }
}

[Trait("Category", TestCategories.Unit)]
public class GodModePresetTests
{
    [Fact]
    public void Default_ShouldHaveNullProperties()
    {
        var preset = new GodModePreset();
        preset.Name.Should().BeNull();
        preset.PowerPlanGuid.Should().BeNull();
        preset.PowerMode.Should().BeNull();
        preset.SourcePowerMode.Should().BeNull();
        preset.EnableOverclocking.Should().BeNull();
        preset.EnableAllCoreCurveOptimizer.Should().BeNull();
        preset.FanFullSpeed.Should().BeNull();
        preset.FanTableInfo.Should().BeNull();
        preset.MinValueOffset.Should().BeNull();
    }

    [Fact]
    public void Init_Properties_ShouldBeRetained()
    {
        var id = Guid.NewGuid();
        var preset = new GodModePreset
        {
            Name = "Performance",
            PowerPlanGuid = id,
            EnableOverclocking = true,
            FanFullSpeed = false,
            MinValueOffset = 5
        };

        preset.Name.Should().Be("Performance");
        preset.PowerPlanGuid.Should().Be(id);
        preset.EnableOverclocking.Should().BeTrue();
        preset.FanFullSpeed.Should().BeFalse();
        preset.MinValueOffset.Should().Be(5);
    }
}
