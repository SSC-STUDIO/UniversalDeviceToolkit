using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Serialization;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Notifications;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Collection(nameof(AutomationUiStepBridgeTests))]
[Trait("Category", TestCategories.Unit)]
public sealed class AutomationUiStepBridgeTests
{
    private static readonly SemaphoreSlim WindowBridgeGate = new(1, 1);

    [Fact]
    public async Task NotificationAutomationStep_IsSupported_WithService_ShouldReturnTrue()
    {
        var step = new NotificationAutomationStep("hello", new FakeAppNotificationService());
        (await step.IsSupportedAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task NotificationAutomationStep_Run_WithText_ShouldShowInfoOnService()
    {
        var notifications = new FakeAppNotificationService();
        var step = new NotificationAutomationStep("Pipeline finished", notifications);

        await step.RunAsync(new AutomationContext(), new AutomationEnvironment(), CancellationToken.None);

        notifications.Titles.Should().Equal("Pipeline finished");
    }

    [Fact]
    public async Task NotificationAutomationStep_Run_ShouldReplaceRunOutput()
    {
        var notifications = new FakeAppNotificationService();
        var step = new NotificationAutomationStep("out=$RUN_OUTPUT$", notifications);
        var ctx = new AutomationContext { LastRunOutput = "42" };

        await step.RunAsync(ctx, new AutomationEnvironment(), CancellationToken.None);

        notifications.Titles.Should().Equal("out=42");
    }

    [Fact]
    public async Task NotificationAutomationStep_Run_WithTextAndNoService_ShouldThrow()
    {
        var step = new NotificationAutomationStep("hello");
        var act = async () => await step.RunAsync(new AutomationContext(), new AutomationEnvironment(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IAppNotificationService*");
    }

    [Fact]
    public async Task NotificationAutomationStep_DeepCopy_ShouldPreserveInjectedService()
    {
        var notifications = new FakeAppNotificationService();
        IAutomationStep step = new NotificationAutomationStep("copy-me", notifications);
        var copy = (NotificationAutomationStep)step.DeepCopy();

        await copy.RunAsync(new AutomationContext(), new AutomationEnvironment(), CancellationToken.None);

        notifications.Titles.Should().Equal("copy-me");
    }

    [Fact]
    public async Task ShowHideMainWindow_IsSupported_WithoutBridge_ShouldReturnFalse()
    {
        await WindowBridgeGate.WaitAsync();
        try
        {
            (await new ShowMainWindowAutomationStep().IsSupportedAsync()).Should().BeFalse();
            (await new HideMainWindowAutomationStep().IsSupportedAsync()).Should().BeFalse();
        }
        finally
        {
            WindowBridgeGate.Release();
        }
    }

    [Fact]
    public async Task ShowHideMainWindow_IsSupported_WhenBridged_ShouldReturnTrue()
    {
        await WindowBridgeGate.WaitAsync();
        try
        {
            using (AutomationWindowVisibility.Register(_ => { }))
            {
                (await new ShowMainWindowAutomationStep().IsSupportedAsync()).Should().BeTrue();
                (await new HideMainWindowAutomationStep().IsSupportedAsync()).Should().BeTrue();
            }
        }
        finally
        {
            WindowBridgeGate.Release();
        }
    }

    [Fact]
    public async Task ShowMainWindow_Run_WhenBridged_ShouldRequestShow()
    {
        await WindowBridgeGate.WaitAsync();
        try
        {
            MainWindowVisibilityAction? seen = null;
            using (AutomationWindowVisibility.Register(action => seen = action))
            {
                await new ShowMainWindowAutomationStep()
                    .RunAsync(new AutomationContext(), new AutomationEnvironment(), CancellationToken.None);
            }

            seen.Should().Be(MainWindowVisibilityAction.Show);
        }
        finally
        {
            WindowBridgeGate.Release();
        }
    }

    [Fact]
    public async Task HideMainWindow_Run_WhenBridged_ShouldRequestHide()
    {
        await WindowBridgeGate.WaitAsync();
        try
        {
            MainWindowVisibilityAction? seen = null;
            using (AutomationWindowVisibility.Register(action => seen = action))
            {
                await new HideMainWindowAutomationStep()
                    .RunAsync(new AutomationContext(), new AutomationEnvironment(), CancellationToken.None);
            }

            seen.Should().Be(MainWindowVisibilityAction.Hide);
        }
        finally
        {
            WindowBridgeGate.Release();
        }
    }

    [Fact]
    public async Task ShowHideMainWindow_Run_WithoutBridge_ShouldThrow()
    {
        await WindowBridgeGate.WaitAsync();
        try
        {
            var ctx = new AutomationContext();
            var env = new AutomationEnvironment();

            var show = async () => await new ShowMainWindowAutomationStep()
                .RunAsync(ctx, env, CancellationToken.None);
            var hide = async () => await new HideMainWindowAutomationStep()
                .RunAsync(ctx, env, CancellationToken.None);

            await show.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not bridged*");
            await hide.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not bridged*");
        }
        finally
        {
            WindowBridgeGate.Release();
        }
    }

    [Fact]
    public void HostEventName_IsWindowVisibility()
    {
        AutomationWindowVisibility.HostEventName.Should().Be("window.visibility");
    }

    [Fact]
    public void NotificationAutomationStep_SerializeRoundTrip_ShouldKeepText()
    {
        var json = AutomationSerialization.SerializeStep(new NotificationAutomationStep("toast body"));
        var restored = AutomationSerialization.DeserializeStep(json).Should().BeOfType<NotificationAutomationStep>().Subject;
        restored.Text.Should().Be("toast body");
    }

    private sealed class FakeAppNotificationService : IAppNotificationService
    {
        public event EventHandler<AppNotificationChangedEventArgs>? Changed;

        public List<string> Titles { get; } = [];

        public Guid Show(AppNotificationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            Titles.Add(request.Title);
            Changed?.Invoke(this, new AppNotificationChangedEventArgs
            {
                Notification = request,
                IsDismiss = false
            });
            return request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        }

        public void Dismiss(Guid id) { }

        public void UpdateProgress(Guid id, double percent, string? message = null) { }

        public Guid ShowSuccess(string title, string? message = null, string? mergeKey = null) =>
            Show(new AppNotificationRequest { Title = title, Message = message, Severity = AppNotificationSeverity.Success, MergeKey = mergeKey });

        public Guid ShowInfo(string title, string? message = null, string? mergeKey = null) =>
            Show(new AppNotificationRequest { Title = title, Message = message, Severity = AppNotificationSeverity.Info, MergeKey = mergeKey });

        public Guid ShowWarning(string title, string? message = null, string? mergeKey = null) =>
            Show(new AppNotificationRequest { Title = title, Message = message, Severity = AppNotificationSeverity.Warning, MergeKey = mergeKey });

        public Guid ShowError(string title, string? message = null, string? mergeKey = null) =>
            Show(new AppNotificationRequest { Title = title, Message = message, Severity = AppNotificationSeverity.Error, MergeKey = mergeKey });
    }
}

[CollectionDefinition(nameof(AutomationUiStepBridgeTests), DisableParallelization = true)]
public sealed class AutomationUiStepBridgeTestsCollection
{
}
