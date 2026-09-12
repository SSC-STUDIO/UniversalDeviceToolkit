using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

/// <summary>
/// Partial-JSON import defaults for fan curves — regression net for schema evolution.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class FanCurveJsonPartialImportTests
{
    [Fact]
    public void ImportFromJson_WithOnlyType_ShouldPreserveDefaults()
    {
        var imported = FanCurveEntry.ImportFromJson("{\"Type\":1}");
        imported.Type.Should().Be(FanType.Gpu);
        imported.CriticalTemp.Should().Be(90);
        imported.MaxPwm.Should().Be(255.0);
    }

    [Fact]
    public void ImportFromJson_WithOnlyCriticalTemp_ShouldPreserveOtherDefaults()
    {
        var imported = FanCurveEntry.ImportFromJson("{\"CriticalTemp\":75}");
        imported.CriticalTemp.Should().Be(75);
        imported.Type.Should().Be(FanType.Cpu);
        imported.MaxPwm.Should().Be(255.0);
    }

    [Fact]
    public void ImportFromJson_WithOnlyMaxPwm_ShouldPreserveOtherDefaults()
    {
        var imported = FanCurveEntry.ImportFromJson("{\"MaxPwm\":200.0}");
        imported.MaxPwm.Should().Be(200.0);
        imported.Type.Should().Be(FanType.Cpu);
        imported.CriticalTemp.Should().Be(90);
    }

    [Fact]
    public void ImportFromJson_WithCurveNodesOnly_ShouldReplaceDefaults()
    {
        var imported = FanCurveEntry.ImportFromJson(
            "{\"CurveNodes\":[{\"Temperature\":35.0,\"TargetPercent\":10},{\"Temperature\":70.0,\"TargetPercent\":60}]}");
        imported.CurveNodes.Should().HaveCount(2);
        imported.CurveNodes[0].Temperature.Should().Be(35.0f);
        imported.CurveNodes[0].TargetPercent.Should().Be(10);
        imported.CurveNodes[1].Temperature.Should().Be(70.0f);
        imported.CurveNodes[1].TargetPercent.Should().Be(60);
    }

    [Fact]
    public void CurveNode_SetTemperatureAndTargetPercent_ShouldUpdateBoth()
    {
        var node = new CurveNode { Temperature = 50.0f, TargetPercent = 30 };
        node.Temperature.Should().Be(50.0f);
        node.TargetPercent.Should().Be(30);
    }
}
