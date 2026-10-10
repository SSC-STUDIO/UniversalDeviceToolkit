using System.Text;
using System.Text.Json;
using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Notifications;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Unit)]
public sealed class AppIntegrationHandlersLifecycleTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(Path.GetTempPath(), $"udt-integration-lifetime-{Guid.NewGuid():N}");
    private readonly EnvironmentVariableScope _settingsScope;
    private readonly FakeNotificationService _notifications = new();

    public AppIntegrationHandlersLifecycleTests()
    {
        _settingsScope = new EnvironmentVariableScope(Folders.AppDataOverrideEnvironmentVariable, _settingsDirectory);
        IoCContainer.Initialize(builder =>
        {
            builder.RegisterType<UpdateCheckSettings>().SingleInstance();
            builder.RegisterType<HttpClientFactory>().SingleInstance();
            builder.RegisterType<UpdateChecker>();
            builder.RegisterInstance(_notifications).As<IAppNotificationService>();
        });
    }

    public void Dispose()
    {
        IoCContainer.Dispose();
        _settingsScope.Dispose();
        if (Directory.Exists(_settingsDirectory))
            Directory.Delete(_settingsDirectory, recursive: true);
    }

    [Fact]
    public void ClosingServer_ReleasesWindowAndNotificationSubscriptions()
    {
        using var server = new BridgeRpcServer(new MemoryStream(), new MemoryStream());
        AppIntegrationHandlers.Register(server);
        AutomationWindowVisibility.IsBridged.Should().BeTrue();
        _notifications.SubscriberCount.Should().Be(1);

        server.Dispose();
        server.Dispose();

        AutomationWindowVisibility.IsBridged.Should().BeFalse();
        _notifications.SubscriberCount.Should().Be(0);
        var request = () => AutomationWindowVisibility.Request(MainWindowVisibilityAction.Show);
        request.Should().Throw<InvalidOperationException>().WithMessage("*not bridged*");
    }

    [Fact]
    public void ClosingOlderServer_PreservesNewerWindowRegistration()
    {
        using var olderOutput = new MemoryStream();
        using var newerOutput = new MemoryStream();
        using var older = new BridgeRpcServer(new MemoryStream(), olderOutput);
        using var newer = new BridgeRpcServer(new MemoryStream(), newerOutput);
        AppIntegrationHandlers.Register(older);
        AppIntegrationHandlers.Register(newer);

        older.Dispose();
        AutomationWindowVisibility.IsBridged.Should().BeTrue();
        AutomationWindowVisibility.Request(MainWindowVisibilityAction.Hide);

        ReadEvents(olderOutput).Should().BeEmpty();
        ReadEvents(newerOutput).Should().Equal(AutomationWindowVisibility.HostEventName);
        Encoding.UTF8.GetString(newerOutput.ToArray()).Should().Contain("\"action\":\"Hide\"");
        newer.Dispose();
        AutomationWindowVisibility.IsBridged.Should().BeFalse();
        _notifications.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void NotificationsAndOsd_AreForwardedOnceToTheirOwningServer()
    {
        using var olderOutput = new MemoryStream();
        using var newerOutput = new MemoryStream();
        using var older = new BridgeRpcServer(new MemoryStream(), olderOutput);
        using var newer = new BridgeRpcServer(new MemoryStream(), newerOutput);
        AppIntegrationHandlers.Register(older);
        AppIntegrationHandlers.Register(newer);

        _notifications.ShowInfo("Before disposal");
        MessagingCenter.Publish(new OsdChangedMessage(OsdState.Show));
        ReadEvents(olderOutput).Should().Equal("notifications.changed", "osd.changed");
        ReadEvents(newerOutput).Should().Equal("notifications.changed", "osd.changed");

        older.Dispose();
        _notifications.SubscriberCount.Should().Be(1);
        _notifications.ShowInfo("After disposal");
        MessagingCenter.Publish(new OsdChangedMessage(OsdState.Hidden));
        ReadEvents(olderOutput).Should().Equal("notifications.changed", "osd.changed");
        ReadEvents(newerOutput).Should().Equal(
            "notifications.changed", "osd.changed", "notifications.changed", "osd.changed");

        newer.Dispose();
        _notifications.SubscriberCount.Should().Be(0);
        _notifications.ShowInfo("After both servers close");
        MessagingCenter.Publish(new OsdChangedMessage(OsdState.Show));
        ReadEvents(newerOutput).Should().HaveCount(4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteringDisposedServer_RejectsRegistrationWithoutChangingLiveSubscriptions(bool hasLiveServer)
    {
        using var active = new BridgeRpcServer(new MemoryStream(), new MemoryStream());
        if (hasLiveServer)
            AppIntegrationHandlers.Register(active);
        using var server = new BridgeRpcServer(new MemoryStream(), new MemoryStream());
        server.Dispose();

        var register = () => AppIntegrationHandlers.Register(server);

        register.Should().Throw<ObjectDisposedException>();
        AutomationWindowVisibility.IsBridged.Should().Be(hasLiveServer);
        _notifications.SubscriberCount.Should().Be(hasLiveServer ? 1 : 0);
    }

    private static string[] ReadEvents(MemoryStream output) => Encoding.UTF8.GetString(output.ToArray())
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line =>
        {
            using var frame = JsonDocument.Parse(line);
            return frame.RootElement.GetProperty("event").GetString()
                ?? throw new InvalidOperationException("Event name is missing.");
        }).ToArray();

    private sealed class FakeNotificationService : IAppNotificationService
    {
        private EventHandler<AppNotificationChangedEventArgs>? _changed;

        public event EventHandler<AppNotificationChangedEventArgs>? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;

        public Guid Show(AppNotificationRequest request)
        {
            _changed?.Invoke(this, new AppNotificationChangedEventArgs { Notification = request });
            return request.Id;
        }

        public void Dismiss(Guid id) { }
        public void UpdateProgress(Guid id, double percent, string? message = null) { }
        public Guid ShowSuccess(string title, string? message = null, string? mergeKey = null) => ShowInfo(title, message, mergeKey);
        public Guid ShowInfo(string title, string? message = null, string? mergeKey = null) =>
            Show(new AppNotificationRequest { Title = title, Message = message, MergeKey = mergeKey });
        public Guid ShowWarning(string title, string? message = null, string? mergeKey = null) => ShowInfo(title, message, mergeKey);
        public Guid ShowError(string title, string? message = null, string? mergeKey = null) => ShowInfo(title, message, mergeKey);
    }
}
