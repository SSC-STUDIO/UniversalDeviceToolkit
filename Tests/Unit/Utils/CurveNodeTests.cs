using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class CurveNodeTests
{
    [Fact]
    public void Temperature_DefaultValue_ShouldBeZero()
    {
        var node = new CurveNode();
        node.Temperature.Should().Be(0f);
    }

    [Fact]
    public void TargetPercent_DefaultValue_ShouldBeZero()
    {
        var node = new CurveNode();
        node.TargetPercent.Should().Be(0);
    }

    [Fact]
    public void Temperature_SetValue_ShouldUpdateAndRaisePropertyChanged()
    {
        var node = new CurveNode();
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurveNode.Temperature))
                raised = true;
        };

        node.Temperature = 75.5f;

        node.Temperature.Should().Be(75.5f);
        raised.Should().BeTrue();
    }

    [Fact]
    public void Temperature_SetSameValue_ShouldNotRaisePropertyChanged()
    {
        var node = new CurveNode();
        node.Temperature = 50f;
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurveNode.Temperature))
                raised = true;
        };

        node.Temperature = 50f;

        raised.Should().BeFalse();
    }

    [Theory]
    [InlineData(50f, 50.0105f, true)]
    [InlineData(50f, 50.01f, false)]
    [InlineData(50f, 50.015f, true)]
    public void Temperature_SmallDelta_ShouldRespectThreshold(float initial, float target, bool shouldFire)
    {
        var node = new CurveNode { Temperature = initial };
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurveNode.Temperature))
                raised = true;
        };

        node.Temperature = target;

        raised.Should().Be(shouldFire);
    }

    [Fact]
    public void TargetPercent_SetValue_ShouldUpdateAndRaisePropertyChanged()
    {
        var node = new CurveNode();
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurveNode.TargetPercent))
                raised = true;
        };

        node.TargetPercent = 80;

        node.TargetPercent.Should().Be(80);
        raised.Should().BeTrue();
    }

    [Fact]
    public void TargetPercent_SetSameValue_ShouldNotRaisePropertyChanged()
    {
        var node = new CurveNode { TargetPercent = 50 };
        var raised = false;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CurveNode.TargetPercent))
                raised = true;
        };

        node.TargetPercent = 50;

        raised.Should().BeFalse();
    }

    [Fact]
    public void Temperature_NegativeValue_ShouldBeAccepted()
    {
        var node = new CurveNode();
        node.Temperature = -10f;
        node.Temperature.Should().Be(-10f);
    }

    [Fact]
    public void TargetPercent_BoundaryValues_ShouldWork()
    {
        var node = new CurveNode();
        node.TargetPercent = 0;
        node.TargetPercent.Should().Be(0);
        node.TargetPercent = 100;
        node.TargetPercent.Should().Be(100);
    }
}
