using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class StepperValueTests
{
    #region WithValue Tests

    [Fact]
    public void WithValue_ShouldReplaceValueOnly()
    {
        var original = new StepperValue(5, 0, 100, 1, [0, 25, 50, 75, 100], 50);
        var updated = original.WithValue(75);

        updated.Value.Should().Be(75);
        updated.Min.Should().Be(0);
        updated.Max.Should().Be(100);
        updated.Step.Should().Be(1);
        updated.DefaultValue.Should().Be(50);
    }

    [Fact]
    public void WithValue_WhenSameValue_ShouldReturnEqualInstance()
    {
        var original = new StepperValue(10, 0, 100, 1, [], null);
        var updated = original.WithValue(10);
        updated.Value.Should().Be(10);
    }

    [Fact]
    public void WithValue_ShouldPreserveSteps()
    {
        int[] steps = [0, 25, 50, 75, 100];
        var original = new StepperValue(0, 0, 100, 1, steps, 50);
        var updated = original.WithValue(50);
        updated.Steps.Should().BeSameAs(steps);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldContainAllFields()
    {
        var sv = new StepperValue(5, 0, 100, 1, [0, 50], 50);
        var text = sv.ToString();
        text.Should().Contain("5").And.Contain("0").And.Contain("100");
    }

    #endregion
}
