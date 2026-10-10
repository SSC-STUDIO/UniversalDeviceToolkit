using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class VersionExtensionsTests
{
    [Fact]
    public void IsBeta_ZeroDotZeroDotOne_ShouldReturnTrue()
    {
        new Version(0, 0, 1, 0).IsBeta().Should().BeTrue();
    }

    [Fact]
    public void IsBeta_Build99_ShouldReturnTrue()
    {
        new Version(1, 2, 99, 0).IsBeta().Should().BeTrue();
    }

    [Fact]
    public void IsBeta_Build99_WithRevision_ShouldReturnTrue()
    {
        new Version(4, 2, 99, 5).IsBeta().Should().BeTrue();
    }

    [Fact]
    public void IsBeta_StableRelease_ShouldReturnFalse()
    {
        new Version(4, 2, 1, 0).IsBeta().Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(10, 0, 19041)]
    public void IsBeta_WithBuildMinusOne_ShouldReturnFalse(int major, int minor, int build)
    {
        new Version(major, minor, build).IsBeta().Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(10, 0, 19041, 0)]
    public void IsBeta_NormalBuild_ShouldReturnFalse(int major, int minor, int build, int revision)
    {
        new Version(major, minor, build, revision).IsBeta().Should().BeFalse();
    }
}
