using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class FanCurveEntryTests
{
    [Fact]
    public void Constructor_ShouldInitializeDefaultCurve()
    {
        var entry = new FanCurveEntry();
        entry.CurveNodes.Should().HaveCount(6);
        entry.CurveNodes[0].Temperature.Should().Be(40);
        entry.CurveNodes[0].TargetPercent.Should().Be(0);
        entry.CurveNodes[5].Temperature.Should().Be(90);
        entry.CurveNodes[5].TargetPercent.Should().Be(100);
    }

    [Fact]
    public void Constructor_DefaultType_ShouldBeCpu()
    {
        var entry = new FanCurveEntry();
        entry.Type.Should().Be(FanType.Cpu);
    }

    [Fact]
    public void Constructor_DefaultCriticalTemp_ShouldBe90()
    {
        var entry = new FanCurveEntry();
        entry.CriticalTemp.Should().Be(90);
    }

    [Fact]
    public void Constructor_DefaultMaxPwm_ShouldBe255()
    {
        var entry = new FanCurveEntry();
        entry.MaxPwm.Should().Be(255.0);
    }

    [Fact]
    public void Type_Set_ShouldFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "Type") fired = true; };
        entry.Type = FanType.Gpu;
        fired.Should().BeTrue();
        entry.Type.Should().Be(FanType.Gpu);
    }

    [Fact]
    public void Type_SameValue_ShouldNotFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "Type") fired = true; };
        entry.Type = FanType.Cpu;
        fired.Should().BeFalse();
    }

    [Fact]
    public void ExportToJson_ShouldContainType()
    {
        var entry = new FanCurveEntry { Type = FanType.Gpu };
        var json = entry.ExportToJson();
        json.Should().Contain("Type");
    }

    [Fact]
    public void ExportJson_ImportFromJson_ShouldRoundTrip()
    {
        var original = new FanCurveEntry
        {
            Type = FanType.Gpu,
            CriticalTemp = 85,
            MaxPwm = 200.0,
            AccelerationDcrReduction = 3,
            DecelerationDcrReduction = 4
        };

        var json = original.ExportToJson();
        var imported = FanCurveEntry.ImportFromJson(json);

        imported.Type.Should().Be(FanType.Gpu);
        imported.CriticalTemp.Should().Be(85);
        imported.MaxPwm.Should().Be(200.0);
        imported.AccelerationDcrReduction.Should().Be(3);
        imported.DecelerationDcrReduction.Should().Be(4);
    }

    [Fact]
    public void ImportFromJson_ShouldPreserveCurveNodes()
    {
        var original = new FanCurveEntry();
        original.CurveNodes.Clear();
        original.CurveNodes.Add(new CurveNode { Temperature = 30, TargetPercent = 10 });
        original.CurveNodes.Add(new CurveNode { Temperature = 70, TargetPercent = 80 });

        var json = original.ExportToJson();
        var imported = FanCurveEntry.ImportFromJson(json);

        imported.CurveNodes.Should().HaveCount(2);
        imported.CurveNodes[0].Temperature.Should().Be(30);
        imported.CurveNodes[0].TargetPercent.Should().Be(10);
    }

    [Fact]
    public void ImportFromJson_Null_ShouldThrow()
    {
        var act = () => FanCurveEntry.ImportFromJson("null");
        act.Should().Throw<System.InvalidOperationException>();
    }

    [Fact]
    public void ExportJson_ShouldBeIndentedJson()
    {
        var entry = new FanCurveEntry();
        var json = entry.ExportToJson();
        json.Should().Contain("\n");
        json.Should().Contain("CurveNodes");
        json.Should().Contain("CriticalTemp");
    }

    [Fact]
    public void ToFanTable_ShouldReturnFanTable()
    {
        var entry = new FanCurveEntry();
        var fanSpeeds = new ushort[] { 0, 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000 };
        var temps = new ushort[] { 30, 40, 50, 60, 70, 80, 85, 90, 95, 100 };
        var tableData = new FanTableData(FanTableType.CPU, 0, 0, fanSpeeds, temps);

        var fanTable = entry.ToFanTable([tableData]);
        fanTable.Should().NotBeNull();
        fanTable.FSS0.Should().BeInRange(0, 9);
    }

    [Fact]
    public void ToFanTable_EmptyData_ShouldThrow()
    {
        var entry = new FanCurveEntry();
        var act = () => entry.ToFanTable([]);
        act.Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void FromFanTableInfo_ShouldCreateEntry()
    {
        var fanSpeeds = new ushort[] { 0, 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000 };
        var temps = new ushort[] { 30, 40, 50, 60, 70, 80, 85, 90, 95, 100 };
        var tableData = new FanTableData(FanTableType.CPU, 0, 0, fanSpeeds, temps);
        var fanTable = new FanTable([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var info = new FanTableInfo([tableData], fanTable);

        var entry = FanCurveEntry.FromFanTableInfo(info, 1);
        entry.Type.Should().Be(FanType.Gpu);
        entry.CurveNodes.Should().NotBeEmpty();
    }
}