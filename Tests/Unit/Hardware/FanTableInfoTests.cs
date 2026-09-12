using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class FanTableInfoStructTests
{
    [Fact]
    public void FanTableInfo_Properties_ShouldReflectConstructor()
    {
        var data = new FanTableData[]
        {
            new(FanTableType.CPU, 0, 0, new ushort[] { 1000, 2000 }, new ushort[] { 40, 50 }),
            new(FanTableType.GPU, 1, 1, new ushort[] { 1500, 2500 }, new ushort[] { 45, 55 })
        };
        var table = new FanTable { FSTM = 1, FSID = 0, FSTL = 0x12345678 };
        var info = new FanTableInfo(data, table);

        info.Data.Should().HaveCount(2);
        info.Data[0].Type.Should().Be(FanTableType.CPU);
        info.Data[1].Type.Should().Be(FanTableType.GPU);
        info.Table.FSTM.Should().Be(1);
        info.Table.FSTL.Should().Be(0x12345678);
    }

    [Fact]
    public void FanTableInfo_ToString_ShouldContainCPU()
    {
        var data = new FanTableData[]
        {
            new(FanTableType.CPU, 0, 0, new ushort[] { 1000 }, new ushort[] { 40 })
        };
        var table = new FanTable();
        var info = new FanTableInfo(data, table);
        info.ToString().Should().Contain("CPU");
    }

    [Fact]
    public void FanTableInfo_EmptyData_ShouldWork()
    {
        var info = new FanTableInfo([], new FanTable());
        info.Data.Should().BeEmpty();
    }
}

