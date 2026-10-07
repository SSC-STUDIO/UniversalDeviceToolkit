using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class WindowBehaviorTests
{
    private static JsonElement Settings(bool minimizeToTray, bool minimizeOnClose = false) => JsonSerializer.SerializeToElement(new
    {
        value = new { MinimizeToTray = minimizeToTray, MinimizeOnClose = minimizeOnClose }
    });

    [Fact]
    public async Task ApplicationSettingEvents_ApplyRuntimeTrayPreferenceChanges()
    {
        var setting = true;
        var calls = 0;
        var behavior = new WindowBehavior(() =>
        {
            calls++;
            return Task.FromResult(Settings(setting));
        }, _ => { });
        var changed = JsonSerializer.SerializeToElement(new { scope = "application" });

        await behavior.OnHostEventAsync("host.ready", default);
        Assert.True(behavior.MinimizeToTray);
        setting = false;
        await behavior.OnHostEventAsync("settings.changed", changed);
        Assert.False(behavior.MinimizeToTray);
        setting = true;
        await behavior.OnHostEventAsync("settings.changed", changed);
        Assert.True(behavior.MinimizeToTray);
        await behavior.OnHostEventAsync("settings.changed", JsonSerializer.SerializeToElement(new { scope = "osd" }));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task MinimizeOnClose_HidesCloseWithoutChangingNormalMinimizeBehavior()
    {
        var minimizeOnClose = true;
        var behavior = new WindowBehavior(() => Task.FromResult(Settings(false, minimizeOnClose)), _ => { });
        await behavior.RefreshAsync();
        Assert.False(behavior.MinimizeToTray);
        Assert.True(behavior.ShouldHideOnClose);

        minimizeOnClose = false;
        await behavior.OnHostEventAsync("settings.changed", JsonSerializer.SerializeToElement(new { scope = "application" }));
        Assert.False(behavior.MinimizeToTray);
        Assert.False(behavior.ShouldHideOnClose);
    }

    [Fact]
    public async Task FailedSettingsRefresh_PreservesPriorPreferenceAndLogsError()
    {
        var fail = false;
        var errors = new List<string>();
        var behavior = new WindowBehavior(() => fail
            ? Task.FromException<JsonElement>(new IOException("host unavailable"))
            : Task.FromResult(Settings(false)), errors.Add);
        await behavior.RefreshAsync();
        fail = true;
        await behavior.RefreshAsync();

        Assert.False(behavior.MinimizeToTray);
        Assert.Contains("host unavailable", Assert.Single(errors));
    }

    [Fact]
    public async Task LateOlderSettingsResponse_CannotReplaceLatestPreference()
    {
        var first = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var behavior = new WindowBehavior(() => ++calls == 1 ? first.Task : second.Task, _ => { });
        var oldRequest = behavior.RefreshAsync();
        var latestRequest = behavior.RefreshAsync();
        second.SetResult(Settings(false));
        await latestRequest;
        first.SetResult(Settings(true));
        await oldRequest;

        Assert.False(behavior.MinimizeToTray);
    }

    [Fact]
    public async Task FailedLatestRefresh_DoesNotAllowOlderResponseToReplaceAcceptedPreference()
    {
        var first = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var behavior = new WindowBehavior(() => (++calls) switch
        {
            1 => Task.FromResult(Settings(false)),
            2 => first.Task,
            _ => second.Task
        }, _ => { });
        await behavior.RefreshAsync();
        var oldRequest = behavior.RefreshAsync();
        var latestRequest = behavior.RefreshAsync();
        second.SetException(new TimeoutException("host timeout"));
        await latestRequest;
        first.SetResult(Settings(true));
        await oldRequest;

        Assert.False(behavior.MinimizeToTray);
    }

    [Fact]
    public async Task HostRestart_RequiresSendingUnchangedHiddenUiActivityAgain()
    {
        var behavior = new WindowBehavior(() => Task.FromResult(Settings(true)), _ => { });
        Assert.True(behavior.TryUpdateUiActivity(false));
        Assert.False(behavior.TryUpdateUiActivity(false));

        await behavior.OnHostEventAsync("host.ready", default);

        Assert.True(behavior.TryUpdateUiActivity(false));
        Assert.False(behavior.TryUpdateUiActivity(false));
        Assert.True(behavior.TryUpdateUiActivity(true));
    }
}
