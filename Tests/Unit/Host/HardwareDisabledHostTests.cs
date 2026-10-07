using System.Text.Json;
using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System.Driver;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Unit)]
public sealed class HardwareDisabledHostTests : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(Path.GetTempPath(), $"udt-hardware-disabled-{Guid.NewGuid():N}");
    private readonly EnvironmentVariableScope _settingsScope;

    public HardwareDisabledHostTests()
    {
        _settingsScope = new EnvironmentVariableScope(Folders.AppDataOverrideEnvironmentVariable, _settingsDirectory);
        IoCContainer.Initialize(
            builder => builder.RegisterInstance(new ApplicationSettings()).AsSelf().SingleInstance(),
            new HardwareDisabledModule());
    }

    public void Dispose()
    {
        IoCContainer.Dispose();
        _settingsScope.Dispose();
        if (Directory.Exists(_settingsDirectory))
            Directory.Delete(_settingsDirectory, recursive: true);
    }

    [Fact]
    public void Container_DoesNotRegisterHardwareOrAutoActivatedListeners()
    {
        IoCContainer.TryResolve<IDriverWrapper>().Should().BeNull();
        IoCContainer.TryResolve<IWMIWrapper>().Should().BeNull();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
        IoCContainer.TryResolve<SensorsGroupController>().Should().BeNull();
        IoCContainer.TryResolve<PowerModeListener>().Should().BeNull();
        IoCContainer.TryResolve<NativeWindowsMessageListener>().Should().BeNull();
        IoCContainer.Resolve<OsdSettings>().Store.Should().NotBeNull();
    }

    [Fact]
    public void Registry_HandlesEveryDomainWithoutResolvingProductionHardware()
    {
        using var rpc = new BridgeRpcServer();
        HardwareDisabledHandlers.Register(rpc);

        foreach (var method in RpcMethodNames.PortableCapable.Concat(RpcMethodNames.WindowsOnly))
            rpc.HasHandler(method).Should().BeTrue($"{method} must have an explicit no-hardware response");

        rpc.HasHandler("host.getCapabilities").Should().BeTrue();
        rpc.HasHandler("macro.play").Should().BeTrue();
        IoCContainer.TryResolve<GPUController>().Should().BeNull();
    }

    [Fact]
    public void Initializer_WithNoHardware_DoesNotResolveControllersOrReturnStartSteps()
    {
        using var rpc = new BridgeRpcServer();
        var initializer = new HardwareInitializer(HostFlags.Parse(["--no-hardware"]), rpc);

        var (initializationSteps, serviceStartSteps) = initializer.GetBackgroundInitializationSteps();

        initializationSteps.Should().BeEmpty();
        serviceStartSteps.Should().BeEmpty();
        initializer.SkippedSteps.Should().Contain("fan-manager").And.Contain("gpu-overclock");
    }

    [Theory]
    [InlineData("sensors.subscribe")]
    [InlineData("sensors.subscribeFps")]
    [InlineData("sensors.setSettings")]
    [InlineData("feature.setState")]
    [InlineData("dashboardHardware.setMonitoring")]
    [InlineData("dashboardHardware.setOverclock")]
    [InlineData("rgb.setState")]
    [InlineData("spectrum.setProfile")]
    [InlineData("driver.install")]
    [InlineData("optimization.apply")]
    [InlineData("automation.runNow")]
    [InlineData("macro.play")]
    [InlineData("app.setAutorun")]
    [InlineData("network.start")]
    public async Task HardwareActions_AreRejectedWithoutARegisteredHardwareBackend(string method)
    {
        var result = await HardwareDisabledHandlers.HandleRequestAsync(Request(method), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.ErrorCode.Should().Be(BridgeErrorCodes.PlatformNotSupported);
    }

    [Theory]
    [InlineData("sensors.getSnapshot")]
    [InlineData("sensors.getDetailed")]
    public async Task SensorRead_ReturnsAnExplicitUnavailableSnapshot(string method)
    {
        var result = await HardwareDisabledHandlers.HandleRequestAsync(Request(method), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var snapshot = JsonSerializer.SerializeToElement(result.Value);
        snapshot.GetProperty("initialized").GetBoolean().Should().BeFalse();
        snapshot.GetProperty("source").GetString().Should().Be("hardware-disabled");
        snapshot.GetProperty("cpu").GetProperty("temperature").ValueKind.Should().Be(JsonValueKind.Null);
        snapshot.GetProperty("gpu").GetProperty("usage").ValueKind.Should().Be(JsonValueKind.Null);
        snapshot.GetProperty("battery").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DashboardRead_DoesNotProbeGpuOrAdvertiseMonitorControl()
    {
        var result = await HardwareDisabledHandlers.HandleRequestAsync(Request("dashboardHardware.getState"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var state = JsonSerializer.SerializeToElement(result.Value);
        state.GetProperty("discreteGpu").GetProperty("supported").GetBoolean().Should().BeFalse();
        state.GetProperty("overclockDiscreteGpu").GetProperty("supported").GetBoolean().Should().BeFalse();
        state.GetProperty("turnOffMonitors").GetProperty("supported").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SensorSettings_CanBeReadWithoutInstantiatingSensorControllers()
    {
        var result = await HardwareDisabledHandlers.HandleRequestAsync(Request("sensors.getSettings"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var settings = JsonSerializer.SerializeToElement(result.Value);
        settings.GetProperty("osdRefreshIntervalSec").GetDouble().Should().Be(1);
        IoCContainer.TryResolve<SensorsGroupController>().Should().BeNull();
    }

    [Fact]
    public async Task ConfigurationChanges_DoNotResolveTheRuntimeIntegrationService()
    {
        var action = () => SettingsHandlers.ApplyIntegrationsLifecycleAsync("integrations", applyRuntimeChanges: false);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public void CapabilityManifest_DoesNotClaimHardwareBackendsOrWriteCapabilities()
    {
        var manifest = JsonSerializer.SerializeToElement(HardwareDisabledHandlers.BuildCapabilities());

        manifest.GetProperty("vendorHardware").GetBoolean().Should().BeFalse();
        var capabilities = manifest.GetProperty("capabilities");
        capabilities.GetProperty("settings").GetBoolean().Should().BeTrue();
        capabilities.GetProperty("sensors").GetBoolean().Should().BeFalse();
        capabilities.GetProperty("keyboard").GetBoolean().Should().BeFalse();
        capabilities.GetProperty("driver").GetBoolean().Should().BeFalse();
        manifest.GetProperty("backends").GetProperty("sensorBackend").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Read_CancellationStillPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => HardwareDisabledHandlers.HandleRequestAsync(Request("sensors.getStatus"), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static BridgeRequest Request(string method)
    {
        using var parameters = JsonDocument.Parse("{}");
        return new BridgeRequest(1, method, parameters.RootElement.Clone());
    }
}
