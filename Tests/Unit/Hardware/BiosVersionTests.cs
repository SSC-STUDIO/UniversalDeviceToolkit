using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class BiosVersionTests
{
    #region IsHigherOrEqualThan Tests

    [Fact]
    public void IsHigherOrEqualThan_WhenSamePrefixHigherVersion_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", 30);
        var other = new BiosVersion("KFCN", 20);
        current.IsHigherOrEqualThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenSamePrefixSameVersion_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", 20);
        var other = new BiosVersion("KFCN", 20);
        current.IsHigherOrEqualThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenSamePrefixLowerVersion_ShouldReturnFalse()
    {
        var current = new BiosVersion("KFCN", 10);
        var other = new BiosVersion("KFCN", 20);
        current.IsHigherOrEqualThan(other).Should().BeFalse();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenDifferentPrefix_ShouldReturnFalse()
    {
        var current = new BiosVersion("KFCN", 30);
        var other = new BiosVersion("GKCN", 20);
        current.IsHigherOrEqualThan(other).Should().BeFalse();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenCaseDifferentPrefix_ShouldStillMatch()
    {
        var current = new BiosVersion("kfcn", 30);
        var other = new BiosVersion("KFCN", 20);
        current.IsHigherOrEqualThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenCurrentVersionNull_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", null);
        var other = new BiosVersion("KFCN", 20);
        current.IsHigherOrEqualThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenOtherVersionNull_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", 20);
        var other = new BiosVersion("KFCN", null);
        current.IsHigherOrEqualThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsHigherOrEqualThan_WhenBothVersionNull_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", null);
        var other = new BiosVersion("KFCN", null);
        current.IsHigherOrEqualThan(other).Should().BeTrue();
    }

    #endregion

    #region IsLowerThan Tests

    [Fact]
    public void IsLowerThan_WhenSamePrefixLowerVersion_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", 10);
        var other = new BiosVersion("KFCN", 20);
        current.IsLowerThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsLowerThan_WhenSamePrefixSameVersion_ShouldReturnFalse()
    {
        var current = new BiosVersion("KFCN", 20);
        var other = new BiosVersion("KFCN", 20);
        current.IsLowerThan(other).Should().BeFalse();
    }

    [Fact]
    public void IsLowerThan_WhenSamePrefixHigherVersion_ShouldReturnFalse()
    {
        var current = new BiosVersion("KFCN", 30);
        var other = new BiosVersion("KFCN", 20);
        current.IsLowerThan(other).Should().BeFalse();
    }

    [Fact]
    public void IsLowerThan_WhenDifferentPrefix_ShouldReturnFalse()
    {
        var current = new BiosVersion("KFCN", 10);
        var other = new BiosVersion("GKCN", 20);
        current.IsLowerThan(other).Should().BeFalse();
    }

    [Fact]
    public void IsLowerThan_WhenCurrentVersionNull_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", null);
        var other = new BiosVersion("KFCN", 20);
        current.IsLowerThan(other).Should().BeTrue();
    }

    [Fact]
    public void IsLowerThan_WhenOtherVersionNull_ShouldReturnTrue()
    {
        var current = new BiosVersion("KFCN", 20);
        var other = new BiosVersion("KFCN", null);
        current.IsLowerThan(other).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var version = new BiosVersion("KFCN", 30);
        version.ToString().Should().Contain("KFCN").And.Contain("30");
    }

    #endregion
}
