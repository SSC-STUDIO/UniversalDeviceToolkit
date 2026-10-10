using FluentAssertions;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.Automation.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

public class PipelineNameLocalizerTests
{
    [Theory]
    [InlineData("Deactivate GPU")]
    [InlineData("Deaktiviere GPU")]
    [InlineData("停用 GPU")]
    public void LocalizeStoredName_KnownBakedTitles_ReturnsCurrentCultureTitle(string baked)
    {
        PipelineNameLocalizer.LocalizeStoredName(baked)
            .Should().Be(Resource.DeactivateGpuQuickAction_Title);
    }

    [Fact]
    public void LocalizeStoredName_StableKey_ReturnsCurrentCultureTitle()
    {
        PipelineNameLocalizer.LocalizeStoredName(PipelineNameLocalizer.DeactivateGpuQuickActionStableName)
            .Should().Be(Resource.DeactivateGpuQuickAction_Title);
    }

    [Fact]
    public void LocalizeStoredName_StableKey_NeverReturnsStorageKey()
    {
        var display = PipelineNameLocalizer.LocalizeStoredName(PipelineNameLocalizer.DeactivateGpuQuickActionStableName);
        display.Should().NotBeNullOrWhiteSpace();
        display.Should().NotBe(PipelineNameLocalizer.DeactivateGpuQuickActionStableName);
        display.Should().NotContain("quickAction.deactivateGpu");
    }

    [Fact]
    public void ResolveDeactivateGpuTitle_IsNonEmptyHumanReadable()
    {
        var title = PipelineNameLocalizer.ResolveDeactivateGpuTitle();
        title.Should().NotBeNullOrWhiteSpace();
        title.Should().NotBe(PipelineNameLocalizer.DeactivateGpuQuickActionStableName);
    }

    [Fact]
    public void LocalizeStoredName_UserCustomName_Unchanged()
    {
        PipelineNameLocalizer.LocalizeStoredName("My custom QA")
            .Should().Be("My custom QA");
    }

    [Fact]
    public void MigrateBakedDefaultNames_RewritesKnownTitles()
    {
        var pipelines = new[]
        {
            new AutomationPipeline { Name = "Deaktiviere GPU" },
            new AutomationPipeline { Name = "Keep me" },
        };

        PipelineNameLocalizer.MigrateBakedDefaultNames(pipelines).Should().BeTrue();
        pipelines[0].Name.Should().Be(PipelineNameLocalizer.DeactivateGpuQuickActionStableName);
        pipelines[1].Name.Should().Be("Keep me");
    }
}
