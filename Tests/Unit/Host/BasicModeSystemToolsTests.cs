using System.Text;
using System.Text.Json;
using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.CLI;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Serialization;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Features.CursorPointer;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Notifications;
using UniversalDeviceToolkit.Lib.Optimization;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System.Driver;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Unit)]
public sealed class BasicModeSystemToolsTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(Path.GetTempPath(), $"udt-basic-tools-{Guid.NewGuid():N}");
    private readonly EnvironmentVariableScope _settingsScope;

    public BasicModeSystemToolsTests()
    {
        _settingsScope = new EnvironmentVariableScope(Folders.AppDataOverrideEnvironmentVariable, _settingsDirectory);
        Log.ResetForTests();
        IoCContainer.Initialize(
            builder => builder.RegisterInstance(new ApplicationSettings()).AsSelf().SingleInstance(),
            new HardwareDisabledModule(allowSystemTools: true));
    }

    public void Dispose()
    {
        IoCContainer.Dispose();
        Log.ResetForTests();
        _settingsScope.Dispose();
        if (Directory.Exists(_settingsDirectory))
            Directory.Delete(_settingsDirectory, recursive: true);
    }

    [Fact]
    public void BasicContainer_ResolvesSystemToolsWithoutRegisteringHardware()
    {
        IoCContainer.Resolve<WindowsOptimizationService>().GetCategories().Should().NotBeEmpty();
        IoCContainer.Resolve<WindowsCleanupService>().Should().NotBeNull();
        IoCContainer.Resolve<PackageDownloaderFactory>().GetInstance(PackageDownloaderFactory.Type.PCSupport).Should().NotBeNull();
        IoCContainer.Resolve<PackageDownloaderFactory>().GetInstance(PackageDownloaderFactory.Type.Vantage).Should().NotBeNull();
        IoCContainer.Resolve<AutomationProcessor>().Should().NotBeNull();
        IoCContainer.Resolve<ICliHostLifecycle>().Should().BeOfType<IpcServer>();

        IoCContainer.TryResolve<IDriverWrapper>().Should().BeNull();
        IoCContainer.TryResolve<IWMIWrapper>().Should().BeNull();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
        IoCContainer.TryResolve<SensorsGroupController>().Should().BeNull();
        IoCContainer.TryResolve<PowerModeListener>().Should().BeNull();
        IoCContainer.TryResolve<NativeWindowsMessageListener>().Should().BeNull();
    }

    [Fact]
    public void BasicCapabilities_PreserveSystemToolsAndRejectVendorHardware()
    {
        var manifest = JsonSerializer.SerializeToElement(HardwareDisabledHandlers.BuildCapabilities(allowSystemTools: true));
        var capabilities = manifest.GetProperty("capabilities");

        manifest.GetProperty("executionMode").GetString().Should().Be("basic");
        foreach (var capability in new[] { "optimization", "cleanup", "network", "driver", "automation", "autorun", "macro", "updates" })
            capabilities.GetProperty(capability).GetBoolean().Should().BeTrue();
        foreach (var capability in new[] { "sensors", "keyboard", "gpuManagement", "godMode", "fanControl" })
            capabilities.GetProperty(capability).GetBoolean().Should().BeFalse();

        var supported = manifest.GetProperty("implementedMethods").EnumerateArray().Select(value => value.GetString()).ToArray();
        supported.Should().Contain("optimization.apply").And.Contain("driver.getPackages").And.Contain("automation.runNow");
        var unsupported = manifest.GetProperty("unsupportedMethods").EnumerateArray().Select(value => value.GetString()).ToArray();
        unsupported.Should().Contain("rgb.setState").And.NotContain("driver.getPackages");
    }

    [Fact]
    public async Task CapabilityRpc_ReportsBasicModeAndKeepsHardwareDisabled()
    {
        var cursorSettings = new CursorPointerSettings();
        cursorSettings.Store.CursorThemeMode = (int)CursorThemeMode.WindowsDefault;
        cursorSettings.Store.LegacyImportDone = true;
        cursorSettings.SynchronizeStore();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"host.getCapabilities\",\"params\":{}}\n"));
        using var output = new MemoryStream();
        using var rpc = new BridgeRpcServer(input, output);
        HardwareDisabledHandlers.Register(rpc, allowSystemTools: true);

        await rpc.RunAsync();

        using var response = JsonDocument.Parse(output.ToArray());
        response.RootElement.GetProperty("id").GetInt64().Should().Be(1);
        var manifest = response.RootElement.GetProperty("result");
        manifest.GetProperty("executionMode").GetString().Should().Be("basic");
        manifest.GetProperty("capabilities").GetProperty("driver").GetBoolean().Should().BeTrue();
        manifest.GetProperty("vendorHardware").GetBoolean().Should().BeFalse();
        manifest.GetProperty("backends").GetProperty("sensorBackend").GetBoolean().Should().BeFalse();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Fact]
    public void BasicInitializer_ReturnsOnlySystemStartupWork()
    {
        using var diagnostic = new EnvironmentVariableScope("UDT_DIAGNOSTIC_MODE", "0");
        using var rpc = new BridgeRpcServer();
        var initializer = new HardwareInitializer(HostFlags.Parse(["--no-hardware"]), rpc);

        var (hardwareSteps, systemSteps) = initializer.GetBackgroundInitializationSteps();

        hardwareSteps.Should().BeEmpty();
        systemSteps.Should().ContainSingle();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Fact]
    public void StrictDiagnostics_DoNotRegisterBasicSystemExecutors()
    {
        IoCContainer.Dispose();
        IoCContainer.Initialize(
            builder => builder.RegisterInstance(new ApplicationSettings()).AsSelf().SingleInstance(),
            new HardwareDisabledModule());

        IoCContainer.TryResolve<WindowsOptimizationService>().Should().BeNull();
        IoCContainer.TryResolve<PackageDownloaderFactory>().Should().BeNull();
        IoCContainer.TryResolve<AutomationProcessor>().Should().BeNull();
        IoCContainer.TryResolve<ICliHostLifecycle>().Should().BeNull();
        IoCContainer.TryResolve<INetworkAccelerationService>().Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapabilityManifest_PartitionsEveryRegisteredMethod(bool allowSystemTools)
    {
        var cursorSettings = new CursorPointerSettings();
        cursorSettings.Store.CursorThemeMode = (int)CursorThemeMode.WindowsDefault;
        cursorSettings.Store.LegacyImportDone = true;
        cursorSettings.SynchronizeStore();
        using var rpc = new BridgeRpcServer();
        HardwareDisabledHandlers.Register(rpc, allowSystemTools);
        var manifest = JsonSerializer.SerializeToElement(HardwareDisabledHandlers.BuildCapabilities(allowSystemTools));
        var implemented = manifest.GetProperty("implementedMethods").EnumerateArray()
            .Select(value => value.GetString()).OfType<string>().ToArray();
        var unsupported = manifest.GetProperty("unsupportedMethods").EnumerateArray()
            .Select(value => value.GetString()).OfType<string>().ToArray();

        implemented.Should().OnlyHaveUniqueItems();
        unsupported.Should().OnlyHaveUniqueItems();
        implemented.Intersect(unsupported, StringComparer.Ordinal).Should().BeEmpty();
        implemented.Concat(unsupported).Should().BeEquivalentTo(
            rpc.RegisteredMethods.Concat(RpcMethodNames.AlwaysOn).Distinct(StringComparer.Ordinal));

        var systemMethods = rpc.RegisteredMethods.Where(method =>
            method.StartsWith("optimization.", StringComparison.Ordinal)
            || method.StartsWith("cleanup.", StringComparison.Ordinal)
            || method.StartsWith("network.", StringComparison.Ordinal)
            || method.StartsWith("driver.", StringComparison.Ordinal)
            || method.StartsWith("mouse.", StringComparison.Ordinal)
            || method.StartsWith("app.update.", StringComparison.Ordinal)
            || method.StartsWith("automation.", StringComparison.Ordinal)
            || method.StartsWith("macro.", StringComparison.Ordinal)
            || method is "app.getAutorun" or "app.setAutorun");
        foreach (var method in systemMethods)
            implemented.Contains(method, StringComparer.Ordinal).Should().Be(allowSystemTools, method);
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Fact]
    public async Task BasicAutomation_RunsExistingNotificationStepWithoutHardware()
    {
        var received = new List<AppNotificationChangedEventArgs>();
        var notifications = IoCContainer.Resolve<IAppNotificationService>();
        notifications.Changed += (_, args) => received.Add(args);
        var pipeline = new AutomationPipeline("Basic notification")
        {
            Steps = [new NotificationAutomationStep("Basic automation notification")],
        };

        await IoCContainer.Resolve<AutomationProcessor>().RunNowAsync(pipeline);

        received.Should().ContainSingle();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasicManualAutomation_RunsSystemStepsWithoutEvaluatingSavedHardwareTriggers(bool composite)
    {
        var received = new List<AppNotificationChangedEventArgs>();
        var notifications = IoCContainer.Resolve<IAppNotificationService>();
        notifications.Changed += (_, args) => received.Add(args);
        IAutomationPipelineTrigger trigger = new HardwareSensorAutomationPipelineTrigger(
            HardwareSensorMetric.CpuTemperature, HardwareSensorComparison.GreaterThanOrEqual,
            70, TimeSpan.Zero, TimeSpan.Zero);
        if (composite)
            trigger = new AndAutomationPipelineTrigger([trigger, new PowerModeAutomationPipelineTrigger(default)]);
        var pipeline = new AutomationPipeline("Manual system action with a saved hardware trigger")
        {
            Trigger = trigger,
            Steps = [new NotificationAutomationStep("Manually invoked notification")],
        };

        await IoCContainer.Resolve<AutomationProcessor>().RunNowAsync(pipeline);

        received.Should().ContainSingle();
        IoCContainer.TryResolve<SensorsGroupController>().Should().BeNull();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Fact]
    public async Task SavedHardwareStep_DeserializesWithoutResolvingHardwareAndFailsExplicitlyWhenRun()
    {
        var step = AutomationSerialization.DeserializeStep("{\"$type\":\"deactivateGPU\",\"state\":0}");
        step.Should().NotBeNull();
        var pipeline = new AutomationPipeline("Saved hardware action")
        {
            Steps = [step ?? throw new InvalidOperationException("Step was not deserialized.")],
        };
        var action = () => IoCContainer.Resolve<AutomationProcessor>().RunNowAsync(pipeline);

        var exception = await action.Should().ThrowAsync<AggregateException>();
        exception.Which.InnerExceptions.Should().ContainSingle()
            .Which.Should().BeOfType<NotSupportedException>().Which.Message.Should().Contain("NOT_SUPPORTED");
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Fact]
    public void BasicAutomation_FiltersNestedHardwareTriggersBeforeReadingTheirState()
    {
        var hardwareTrigger = new HardwareSensorAutomationPipelineTrigger(
            HardwareSensorMetric.CpuTemperature, HardwareSensorComparison.GreaterThanOrEqual,
            70, TimeSpan.Zero, TimeSpan.Zero);
        AutomationProcessor.SupportsTrigger(hardwareTrigger, hardwareEnabled: false).Should().BeFalse();
        AutomationProcessor.SupportsTrigger(new OnStartupAutomationPipelineTrigger(), hardwareEnabled: false).Should().BeTrue();
        AutomationProcessor.SupportsTrigger(hardwareTrigger, hardwareEnabled: true).Should().BeTrue();
        AutomationProcessor.SupportsTrigger(new AndAutomationPipelineTrigger([new OnStartupAutomationPipelineTrigger(), hardwareTrigger]), hardwareEnabled: false)
            .Should().BeFalse();
    }
}
