using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Trait("Category", TestCategories.Unit)]
public class AutomationStepModelTests
{
    #region RunAutomationStep Tests

    [Fact]
    public void RunAutomationStep_Defaults_ShouldHaveExpectedValues()
    {
        var step = new RunAutomationStep(null, null, null, null);
        step.ScriptPath.Should().BeNull();
        step.ScriptArguments.Should().BeNull();
        step.RunSilently.Should().BeTrue();
        step.WaitUntilFinished.Should().BeFalse();
    }

    [Fact]
    public void RunAutomationStep_SetProperties_ShouldRetainValues()
    {
        var step = new RunAutomationStep("script.bat", "/arg1", false, true);
        step.ScriptPath.Should().Be("script.bat");
        step.ScriptArguments.Should().Be("/arg1");
        step.RunSilently.Should().BeFalse();
        step.WaitUntilFinished.Should().BeTrue();
    }

    [Fact]
    public async Task RunAutomationStep_IsSupported_ShouldReturnTrue()
    {
        var step = new RunAutomationStep("test", null, null, null);
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    [Fact]
    public void RunAutomationStep_DeepCopy_ShouldReturnNewInstance()
    {
        var step = new RunAutomationStep("test.bat", "/arg", true, false);
        var copy = ((IAutomationStep)step).DeepCopy();
        copy.Should().NotBeSameAs(step);
        var runCopy = (RunAutomationStep)copy;
        runCopy.ScriptPath.Should().Be("test.bat");
        runCopy.ScriptArguments.Should().Be("/arg");
    }

    #endregion

    #region NotificationAutomationStep Tests

    [Fact]
    public void NotificationAutomationStep_Text_ShouldRetainValue()
    {
        var step = new NotificationAutomationStep("Hello World");
        step.Text.Should().Be("Hello World");
    }

    [Fact]
    public void NotificationAutomationStep_NullText_ShouldBeNull()
    {
        var step = new NotificationAutomationStep(null);
        step.Text.Should().BeNull();
    }

    [Fact]
    public async Task NotificationAutomationStep_IsSupported_WithoutService_ShouldReturnFalse()
    {
        var step = new NotificationAutomationStep("test");
        (await step.IsSupportedAsync()).Should().BeFalse();
    }

    [Fact]
    public void NotificationAutomationStep_DeepCopy_ShouldReturnNewInstance()
    {
        var step = new NotificationAutomationStep("notification text");
        var copy = ((IAutomationStep)step).DeepCopy();
        copy.Should().NotBeSameAs(step);
        ((NotificationAutomationStep)copy).Text.Should().Be("notification text");
    }

    [Fact]
    public async Task NotificationAutomationStep_Run_NullText_ShouldNotThrow()
    {
        var step = new NotificationAutomationStep(null);
        var ctx = new AutomationContext();
        var env = new AutomationEnvironment();
        var act = async () => await step.RunAsync(ctx, env, System.Threading.CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region PlaySoundAutomationStep Tests

    [Fact]
    public void PlaySoundAutomationStep_Path_ShouldRetainValue()
    {
        var step = new PlaySoundAutomationStep(@"C:\sound.wav");
        step.Path.Should().Be(@"C:\sound.wav");
    }

    [Fact]
    public void PlaySoundAutomationStep_NullPath_ShouldBeNull()
    {
        var step = new PlaySoundAutomationStep(null);
        step.Path.Should().BeNull();
    }

    [Fact]
    public async Task PlaySoundAutomationStep_IsSupported_ShouldReturnTrue()
    {
        var step = new PlaySoundAutomationStep("test");
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    [Fact]
    public void PlaySoundAutomationStep_DeepCopy_ShouldReturnNewInstance()
    {
        var step = new PlaySoundAutomationStep(@"C:\test.wav");
        var copy = step.DeepCopy();
        copy.Should().NotBeSameAs(step);
        ((PlaySoundAutomationStep)copy).Path.Should().Be(@"C:\test.wav");
    }

    [Fact]
    public async Task PlaySoundAutomationStep_Run_NullPath_ShouldNotThrow()
    {
        var step = new PlaySoundAutomationStep(null);
        var ctx = new AutomationContext();
        var env = new AutomationEnvironment();
        var act = async () => await step.RunAsync(ctx, env, System.Threading.CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion

        #region DelayAutomationStep Tests

    [Fact]
    public void DelayAutomationStep_Delay_ShouldRetainValue()
    {
        var step = new DelayAutomationStep(new Delay(5));
        step.State.Should().Be(new Delay(5));
    }

    [Fact]
    public async Task DelayAutomationStep_IsSupported_ShouldReturnTrue()
    {
        var step = new DelayAutomationStep(new Delay(1));
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    [Fact]
    public void DelayAutomationStep_DeepCopy_ShouldReturnNewInstance()
    {
        var step = new DelayAutomationStep(new Delay(10));
        var copy = step.DeepCopy();
        copy.Should().NotBeSameAs(step);
        ((DelayAutomationStep)copy).State.Should().Be(new Delay(10));
    }

    #endregion

    #region TurnOnWiFiAutomationStep Tests

    [Fact]
    public async Task TurnOnWiFi_IsSupported_ShouldReturnTrue()
    {
        var step = new TurnOnWiFiAutomationStep();
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    [Fact]
    public void TurnOnWiFi_DeepCopy_ShouldReturnNewInstance()
    {
        var step = new TurnOnWiFiAutomationStep();
        var copy = step.DeepCopy();
        copy.Should().NotBeSameAs(step);
    }

    #endregion

    #region TurnOffWiFiAutomationStep Tests

    [Fact]
    public async Task TurnOffWiFi_IsSupported_ShouldReturnTrue()
    {
        var step = new TurnOffWiFiAutomationStep();
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    [Fact]
    public void TurnOffWiFi_DeepCopy_ShouldReturnNewInstance()
    {
        var step = new TurnOffWiFiAutomationStep();
        var copy = step.DeepCopy();
        copy.Should().NotBeSameAs(step);
    }

    #endregion

        #region QuickActionAutomationStep Tests

    [Fact]
    public void QuickActionAutomationStep_PipelineId_ShouldRetainValue()
    {
        var id = Guid.NewGuid();
        var step = new QuickActionAutomationStep(id);
        step.PipelineId.Should().Be(id);
    }

    [Fact]
    public void QuickActionAutomationStep_NullPipelineId_ShouldBeNull()
    {
        var step = new QuickActionAutomationStep(null);
        step.PipelineId.Should().BeNull();
    }

    [Fact]
    public async Task QuickActionAutomationStep_IsSupported_ShouldReturnTrue()
    {
        var step = new QuickActionAutomationStep(null);
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    

    #endregion
}