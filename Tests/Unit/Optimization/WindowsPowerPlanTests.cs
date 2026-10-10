using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Optimization;

[Trait("Category", TestCategories.Unit)]
public class WindowsPowerPlanTests
{
    private static readonly Guid TestGuid1 = new("12345678-1234-1234-1234-123456789abc");
    private static readonly Guid TestGuid2 = new("abcdefab-abcd-abcd-abcd-abcdefabcdef");

    [Fact]
    public void Properties_ShouldReturnConstructorValues()
    {
        var plan = new WindowsPowerPlan(TestGuid1, "Balanced", true);

        plan.Guid.Should().Be(TestGuid1);
        plan.Name.Should().Be("Balanced");
        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Equals_SameGuid_ShouldBeEqual()
    {
        var plan1 = new WindowsPowerPlan(TestGuid1, "Balanced", true);
        var plan2 = new WindowsPowerPlan(TestGuid1, "High Performance", false);

        plan1.Equals(plan2).Should().BeTrue();
        (plan1 == plan2).Should().BeTrue();
        (plan1 != plan2).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentGuid_ShouldNotBeEqual()
    {
        var plan1 = new WindowsPowerPlan(TestGuid1, "Balanced", true);
        var plan2 = new WindowsPowerPlan(TestGuid2, "Balanced", true);

        plan1.Equals(plan2).Should().BeFalse();
        (plan1 == plan2).Should().BeFalse();
        (plan1 != plan2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameGuid_ShouldMatch()
    {
        var plan1 = new WindowsPowerPlan(TestGuid1, "Balanced", true);
        var plan2 = new WindowsPowerPlan(TestGuid1, "High Performance", false);

        plan1.GetHashCode().Should().Be(plan2.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentNameSameGuid_ShouldBeEqual()
    {
        var plan1 = new WindowsPowerPlan(TestGuid1, "Balanced", true);
        var plan2 = new WindowsPowerPlan(TestGuid1, "Balanced (new)", false);

        (plan1 == plan2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldContainAllProperties()
    {
        var plan = new WindowsPowerPlan(TestGuid1, "Power Saver", false);
        var str = plan.ToString();

        str.Should().Contain(TestGuid1.ToString());
        str.Should().Contain("Power Saver");
    }
}
