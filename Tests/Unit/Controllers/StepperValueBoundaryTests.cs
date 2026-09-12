using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class StepperValueBoundaryTests
{
    #region WithValue Boundary Tests

    [Fact]
    public void WithValue_AtMin_ShouldSetToMin()
    {
        var sv = new StepperValue(50, 0, 100, 10, [0, 25, 50, 75, 100], 50);
        var result = sv.WithValue(0);
        result.Value.Should().Be(0);
        result.Min.Should().Be(0);
        result.Max.Should().Be(100);
    }

    [Fact]
    public void WithValue_AtMax_ShouldSetToMax()
    {
        var sv = new StepperValue(50, 0, 100, 10, [0, 25, 50, 75, 100], 50);
        var result = sv.WithValue(100);
        result.Value.Should().Be(100);
    }

    [Fact]
    public void WithValue_NegativeValue_ShouldBeAccepted()
    {
        var sv = new StepperValue(10, -50, 50, 5, [-50, 0, 50], 0);
        var result = sv.WithValue(-25);
        result.Value.Should().Be(-25);
    }

    [Fact]
    public void WithValue_BeyondMax_ShouldStillAcceptValue()
    {
        var sv = new StepperValue(50, 0, 100, 10, [], 50);
        var result = sv.WithValue(200);
        result.Value.Should().Be(200);
    }

    [Fact]
    public void WithValue_BelowMin_ShouldStillAcceptValue()
    {
        var sv = new StepperValue(50, 0, 100, 10, [], 50);
        var result = sv.WithValue(-10);
        result.Value.Should().Be(-10);
    }

    #endregion

    #region Empty Steps Tests

    [Fact]
    public void WithValue_EmptyStepsArray_ShouldPreserveEmptyArray()
    {
        var sv = new StepperValue(0, 0, 100, 1, [], null);
        var result = sv.WithValue(50);
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public void ToString_EmptySteps_ShouldContainEmptyBrackets()
    {
        var sv = new StepperValue(10, 0, 100, 5, [], 50);
        sv.ToString().Should().Contain("[]");
    }

    [Fact]
    public void ToString_NullSteps_ShouldContainEmptyBrackets()
    {
        var sv = new StepperValue(10, 0, 100, 5, null!, 50);
        sv.ToString().Should().Contain("[]");
    }

    #endregion

    #region DefaultValue Tests

    [Fact]
    public void DefaultValue_Null_ShouldBeNull()
    {
        var sv = new StepperValue(10, 0, 100, 1, [0, 50, 100], null);
        sv.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void DefaultValue_ShouldBePreservedAfterWithValue()
    {
        var sv = new StepperValue(10, 0, 100, 1, [0, 50, 100], 75);
        sv.WithValue(30).DefaultValue.Should().Be(75);
    }

    [Fact]
    public void ToString_NullDefault_ShouldShowNull()
    {
        var sv = new StepperValue(10, 0, 100, 1, [0], null);
        sv.ToString().Should().Contain("DefaultValue");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldContainAllFieldNames()
    {
        var sv = new StepperValue(42, 0, 100, 5, [0, 25, 50, 75, 100], 50);
        var text = sv.ToString();
        text.Should().Contain("Value").And.Contain("Min").And.Contain("Max")
            .And.Contain("Step").And.Contain("Steps").And.Contain("DefaultValue");
    }

    #endregion
}

