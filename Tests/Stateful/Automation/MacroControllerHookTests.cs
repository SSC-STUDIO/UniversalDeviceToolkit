using System;
using System.Threading.Tasks;
using Moq;
using UniversalDeviceToolkit.Lib.Macro;
using UniversalDeviceToolkit.Lib.Macro.Utils;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Tests;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Collection(TestCollections.Settings)]
public sealed class MacroControllerHookTests : IDisposable
{
    public void Dispose() => MacroController.HookInstallOverride = null;

    private static (MacroController ctrl, Mock<IMainThreadDispatcher> dispatcher) CreateController(bool installSucceeds)
    {
        var settings = new MacroSettings();
        settings.Store.IsEnabled = false;
        var dispatcher = new Mock<IMainThreadDispatcher>(MockBehavior.Strict);
        MacroController.HookInstallOverride = () => installSucceeds;
        var ctrl = new MacroController(settings, dispatcher.Object);
        return (ctrl, dispatcher);
    }

    [Fact]
    public void SetEnabled_True_WaitsForPump_AndDoesNotUseMainThreadDispatcher()
    {
        var (ctrl, dispatcher) = CreateController(installSucceeds: true);
        using (ctrl)
        {
            ctrl.SetEnabled(true);

            Assert.True(ctrl.IsEnabled);
            Assert.True(ctrl.IsHookActive);
            dispatcher.Verify(d => d.Dispatch(It.IsAny<Action>()), Times.Never);
            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<Func<Task>>()), Times.Never);

            ctrl.SetEnabled(false);
            Assert.False(ctrl.IsEnabled);
            Assert.False(ctrl.IsHookActive);
        }
    }

    [Fact]
    public void SetEnabled_True_WhenInstallFails_DoesNotPersistEnabled()
    {
        var (ctrl, _) = CreateController(installSucceeds: false);
        using (ctrl)
        {
            var ex = Assert.Throws<MacroHookInstallException>(() => ctrl.SetEnabled(true));

            Assert.Contains("SetWindowsHookEx", ex.Message, StringComparison.Ordinal);
            Assert.False(ctrl.IsEnabled);
            Assert.False(ctrl.IsHookActive);
        }
    }

    [Fact]
    public void Start_ReturnsFalse_WhenInstallFails()
    {
        var (ctrl, _) = CreateController(installSucceeds: false);
        using (ctrl)
        {
            Assert.False(ctrl.Start());
            Assert.False(ctrl.IsHookActive);
            Assert.False(ctrl.IsEnabled);
        }
    }

    [Fact]
    public void Start_IsIdempotent_WhilePumpIsAlive()
    {
        var (ctrl, _) = CreateController(installSucceeds: true);
        using (ctrl)
        {
            Assert.True(ctrl.Start());
            Assert.True(ctrl.IsHookActive);
            Assert.True(ctrl.Start());
            Assert.True(ctrl.IsHookActive);

            ctrl.Stop();
            Assert.False(ctrl.IsHookActive);
        }
    }

    [Fact]
    public void Constructor_StartsPump_WhenSettingsAlreadyEnabled()
    {
        var settings = new MacroSettings();
        settings.Store.IsEnabled = true;
        var dispatcher = new Mock<IMainThreadDispatcher>(MockBehavior.Strict);
        MacroController.HookInstallOverride = static () => true;
        using var ctrl = new MacroController(settings, dispatcher.Object);

        Assert.True(ctrl.IsEnabled);
        Assert.True(ctrl.IsHookActive);
        dispatcher.Verify(d => d.Dispatch(It.IsAny<Action>()), Times.Never);

        ctrl.SetEnabled(false);
        Assert.False(ctrl.IsHookActive);
        Assert.False(ctrl.IsEnabled);
    }

    [SkippableFact]
    public void SetEnabled_True_RealSetWindowsHookEx_OwnsPumpThread()
    {
        MacroController.HookInstallOverride = null;
        var settings = new MacroSettings();
        settings.Store.IsEnabled = false;
        var dispatcher = new Mock<IMainThreadDispatcher>(MockBehavior.Strict);
        using var ctrl = new MacroController(settings, dispatcher.Object);

        try
        {
            ctrl.SetEnabled(true);
        }
        catch (MacroHookInstallException ex)
        {
            throw Xunit.Sdk.SkipException.ForSkip(ex.Message);
        }

        Assert.True(ctrl.IsEnabled);
        Assert.True(ctrl.IsHookActive);
        dispatcher.Verify(d => d.Dispatch(It.IsAny<Action>()), Times.Never);

        ctrl.SetEnabled(false);
        Assert.False(ctrl.IsEnabled);
        Assert.False(ctrl.IsHookActive);
    }
}
