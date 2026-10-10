using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class WindowsSingleInstanceTests
{
    [Fact]
    public async Task LaunchBeforeListenerStarts_RestoresPrimary()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = $"Local\\UDT.SingleInstance.Test.{Guid.NewGuid():N}";
        using var primary = new SingleInstance(name);
        using (var secondary = new SingleInstance(name))
            Assert.False(secondary.IsPrimary);

        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.Listen(() => activated.TrySetResult());
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(primary.IsPrimary);
    }

    [Fact]
    public async Task SubsequentLaunches_KeepActivatingSamePrimary()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = $"Local\\UDT.SingleInstance.Test.{Guid.NewGuid():N}";
        using var primary = new SingleInstance(name);
        using var activations = new SemaphoreSlim(0);
        primary.Listen(() => activations.Release());
        for (var index = 0; index < 3; index++)
        {
            using var secondary = new SingleInstance(name);
            Assert.False(secondary.IsPrimary);
            Assert.True(await activations.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public void PrimaryExit_AllowsNewSession()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = $"Local\\UDT.SingleInstance.Test.{Guid.NewGuid():N}";
        using (var primary = new SingleInstance(name)) Assert.True(primary.IsPrimary);
        using var next = new SingleInstance(name);
        Assert.True(next.IsPrimary);
    }
}
