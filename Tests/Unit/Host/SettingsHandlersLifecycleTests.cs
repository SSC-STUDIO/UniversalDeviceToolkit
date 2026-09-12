using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class SettingsHandlersLifecycleTests : IDisposable
{
    public void Dispose() => SettingsHandlers.LifecycleOverrideForTests = null;

    [Fact]
    public async Task ApplyIntegrationsLifecycle_IntegrationsScope_StartsPipe()
    {
        var lifecycle = new Mock<ICliHostLifecycle>(MockBehavior.Strict);
        lifecycle.Setup(x => x.StartStopIfNeededAsync()).Returns(Task.CompletedTask);
        SettingsHandlers.LifecycleOverrideForTests = lifecycle.Object;

        await SettingsHandlers.ApplyIntegrationsLifecycleAsync("integrations");

        lifecycle.Verify(x => x.StartStopIfNeededAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyIntegrationsLifecycle_OtherScope_DoesNotTouchPipe()
    {
        var lifecycle = new Mock<ICliHostLifecycle>(MockBehavior.Strict);
        SettingsHandlers.LifecycleOverrideForTests = lifecycle.Object;

        await SettingsHandlers.ApplyIntegrationsLifecycleAsync("application");

        lifecycle.Verify(x => x.StartStopIfNeededAsync(), Times.Never);
    }
}
