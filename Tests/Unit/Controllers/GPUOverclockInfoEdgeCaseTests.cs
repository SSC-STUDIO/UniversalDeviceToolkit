using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GPUOverclockInfoEdgeCaseTests
{
    [Fact]
    public void GPUOverclockInfo_Properties_ShouldReflectConstructor()
    {
        var info = new GPUOverclockInfo(150, 500);
        info.CoreDeltaMhz.Should().Be(150);
        info.MemoryDeltaMhz.Should().Be(500);
    }

    [Fact]
    public void GPUOverclockInfo_ZeroValues_ShouldWork()
    {
        var info = new GPUOverclockInfo(0, 0);
        info.CoreDeltaMhz.Should().Be(0);
        info.MemoryDeltaMhz.Should().Be(0);
    }

    [Fact]
    public void GPUOverclockInfo_NegativeValues_ShouldBeAccepted()
    {
        var info = new GPUOverclockInfo(-50, -200);
        info.CoreDeltaMhz.Should().Be(-50);
        info.MemoryDeltaMhz.Should().Be(-200);
    }

    [Fact]
    public void GPUOverclockInfo_MaxValues_ShouldWork()
    {
        var info = new GPUOverclockInfo(int.MaxValue, int.MaxValue);
        info.CoreDeltaMhz.Should().Be(int.MaxValue);
        info.MemoryDeltaMhz.Should().Be(int.MaxValue);
    }

    [Fact]
    public void GPUOverclockInfo_MinValues_ShouldWork()
    {
        var info = new GPUOverclockInfo(int.MinValue, int.MinValue);
        info.CoreDeltaMhz.Should().Be(int.MinValue);
        info.MemoryDeltaMhz.Should().Be(int.MinValue);
    }
}
