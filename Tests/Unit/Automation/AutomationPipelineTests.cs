using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Trait("Category", TestCategories.Unit)]
public class AutomationPipelineTests
{
    [Fact]
    public void GetAllSteps_WhenQuickActionsFormCycle_ShouldThrow()
    {
        // Arrange
        var pipelineA = new AutomationPipeline("A");
        var pipelineB = new AutomationPipeline("B");

        pipelineA.Steps.Add(new QuickActionAutomationStep(pipelineB.Id));
        pipelineB.Steps.Add(new QuickActionAutomationStep(pipelineA.Id));

        // Act
        Action act = () => InvokeGetAllSteps(pipelineA, [pipelineA, pipelineB]).ToList();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Quick Action cycle*");
    }

    [Fact]
    public void GetAllSteps_WhenQuickActionTargetsLeafPipeline_ShouldExpandSteps()
    {
        // Arrange
        var root = new AutomationPipeline("Root");
        var leaf = new AutomationPipeline("Leaf");
        var leafStep = new TestAutomationStep();

        root.Steps.Add(new QuickActionAutomationStep(leaf.Id));
        leaf.Steps.Add(leafStep);

        // Act
        var steps = InvokeGetAllSteps(root, [root, leaf]).ToList();

        // Assert
        steps.Should().ContainSingle(step => ReferenceEquals(step, leafStep));
        steps.Should().ContainSingle(step => step is QuickActionAutomationStep);
    }

    private static IEnumerable<IAutomationStep> InvokeGetAllSteps(AutomationPipeline pipeline, List<AutomationPipeline> pipelines) =>
        (IEnumerable<IAutomationStep>)typeof(AutomationPipeline)
            .GetMethod("GetAllSteps", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(List<AutomationPipeline>)], null)!
            .Invoke(pipeline, [pipelines])!;

    private sealed class TestAutomationStep : IAutomationStep
    {
        public Task<bool> IsSupportedAsync() => Task.FromResult(true);

        public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => Task.CompletedTask;

        public IAutomationStep DeepCopy() => new TestAutomationStep();
    }
}
