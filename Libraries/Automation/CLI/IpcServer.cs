using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.CLI.Lib;
using UniversalDeviceToolkit.CLI.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Utils;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Lib.Automation.CLI.Features;

namespace UniversalDeviceToolkit.Lib.Automation.CLI;

public class IpcServer(
    AutomationProcessor automationProcessor,
    SpectrumKeyboardBacklightController spectrumKeyboardBacklightController,
    RGBKeyboardBacklightController rgbKeyboardBacklightController,
    IntegrationsSettings settings,
    UpdateChecker updateChecker,
    UpdateCheckSettings updateCheckSettings,
    INetworkAccelerationService networkAccelerationService,
    INetworkDiagnosticsService networkDiagnosticsService,
    INetworkStateRecoveryService networkStateRecoveryService
    ) : ICliHostLifecycle
{


    private static readonly SemaphoreSlim SupportedFeaturesCacheSemaphore = new(1, 1);
    private static string? _supportedFeaturesCache;

    private readonly object _gate = new();
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task _handler = Task.CompletedTask;

    public async Task StartStopIfNeededAsync()
    {
        await StopAsync().ConfigureAwait(false);

        if (!settings.Store.CLI)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting...");

        CancellationTokenSource newCts;
        lock (_gate)
        {
            if (_cancellationTokenSource is not null)
            {
                try { _cancellationTokenSource.Dispose(); }
                catch (ObjectDisposedException) { /* already disposed */ }
            }
            newCts = new CancellationTokenSource();
            _cancellationTokenSource = newCts;
            _handler = Task.Run(() => Handler(newCts.Token), newCts.Token);
        }

        if (Log.Instance.IsTraceEnabled)
        {
            var pipeNames = GetPipeNames();
            Log.Instance.Trace($"Started (listening on: {string.Join(", ", pipeNames)})");
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource cts;
        Task handler;

        lock (_gate)
        {
            cts = _cancellationTokenSource;
            handler = _handler;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping...");

        try
        {
            try
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already stopped and disposed by a prior StopAsync call.
            }

            var completed = await Task.WhenAny(handler, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);
            if (completed != handler && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"IPC server handler did not stop within timeout.");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cancellationTokenSource, cts))
                {
                    try { cts.Dispose(); }
                    catch (ObjectDisposedException) { /* already disposed */ }

                    _cancellationTokenSource = new CancellationTokenSource();
                    _handler = Task.CompletedTask;
                }
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped");
    }

    private async Task Handler(CancellationToken token)
    {
        var pipeNames = GetPipeNames();
        var acceptLoops = pipeNames
            .Select(pipeName => HandlerForPipe(pipeName, token))
            .ToArray();

        await Task.WhenAll(acceptLoops).ConfigureAwait(false);
    }

    private async Task HandlerForPipe(string pipeName, CancellationToken token)
    {
        try
        {
            await using var pipe = CreatePipeServerStream(pipeName);

            while (!token.IsCancellationRequested)
            {
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Connection received. [pipe={pipeName}]");

                try
                {
                    pipe.ReadMode = PipeTransmissionMode.Message;

                    var challenge = RandomNumberGenerator.GetBytes(32);
                    var encryptedChallenge = ProtectedData.Protect(challenge, null, DataProtectionScope.CurrentUser);
                    var challengeResponse = new IpcResponse { Success = true, Message = Convert.ToHexString(encryptedChallenge) };
                    await pipe.WriteObjectAsync(challengeResponse, token).ConfigureAwait(false);

                    var req = await pipe.ReadObjectAsync<IpcRequest>(token).ConfigureAwait(false);

                    if (req?.Operation is null)
                        throw new IpcException("Failed to deserialize request");

                    if (string.IsNullOrWhiteSpace(req.AuthToken))
                        throw new IpcException("Unauthorized");

                    try
                    {
                        var clientResponse = Convert.FromHexString(req.AuthToken);
                        var decryptedChallenge = ProtectedData.Unprotect(clientResponse, null, DataProtectionScope.CurrentUser);
                        if (!challenge.AsSpan().SequenceEqual(decryptedChallenge))
                            throw new IpcException("Unauthorized");
                    }
                    catch (Exception ex) when (ex is FormatException or CryptographicException)
                    {
                        throw new IpcException("Unauthorized");
                    }

                    EnsurePeerElevation(pipe, IsCurrentProcessElevated());

                    var res = await HandleRequest(req).ConfigureAwait(false);
                    await pipe.WriteObjectAsync(res, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var res = new IpcResponse { Success = false, Message = ex.Message };
                    await pipe.WriteObjectAsync(res, token).ConfigureAwait(false);
                }
                finally
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Disconnecting... [pipe={pipeName}]");

                    pipe.Disconnect();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when server is shutting down, no action needed
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unknown failure. [pipe={pipeName}]", ex);
        }
    }

    private static NamedPipeServerStream CreatePipeServerStream(string pipeName)
    {
        var security = CreatePipeSecurity();
        return NamedPipeServerStreamAcl.Create(pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Message,
            PipeOptions.None,
            0,
            0,
            security);
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var adminIdentity = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        security.AddAccessRule(new(adminIdentity, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        if (WindowsIdentity.GetCurrent().User is { } currentUser)
            security.AddAccessRule(new(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return security;
    }

    private static void EnsurePeerElevation(NamedPipeServerStream pipe, bool serverElevated)
    {
        if (!serverElevated)
            return;

        var peerElevated = false;
        pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            peerElevated = IsAdministratorToken(identity);
        });

        if (!IsPeerElevationAllowed(serverElevated, peerElevated))
            throw new IpcException("Unauthorized");
    }

    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return IsAdministratorToken(identity);
    }

    private static bool IsAdministratorToken(WindowsIdentity identity) =>
        new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);

    private static bool IsPeerElevationAllowed(bool serverElevated, bool peerElevated) =>
        !serverElevated || peerElevated;

    /// <summary>
    /// Dual listen names: legacy DEFAULT (primary) + preferred UDT, both isolation-suffixed the same way.
    /// </summary>
    private static string[] GetPipeNames()
    {
#if UDT_TEST_HOOKS
        var isolationPath = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        return UniversalDeviceToolkit.CLI.Lib.Constants.GetServerPipeNames(isolationPath);
#else
        return UniversalDeviceToolkit.CLI.Lib.Constants.GetServerPipeNames();
#endif
    }

    private async Task<IpcResponse> HandleRequest(IpcRequest req)
    {
        string? message;

        switch (req.Operation)
        {
            case IpcRequest.OperationType.ListQuickActions:
                message = await ListQuickActionsAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.QuickAction when req is { Name: not null }:
                await RunQuickActionAsync(req.Name).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.ListFeatures:
                message = await ListFeaturesAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.ListFeatureValues when req is { Name: not null }:
                message = await ListFeatureValuesAsync(req.Name).ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.GetFeatureValue when req is { Name: not null }:
                message = await GetFeatureValueAsync(req.Name).ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetFeatureValue when req is { Name: not null, Value: not null }:
                await SetFeatureValueAsync(req.Name, req.Value).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetSpectrumProfile:
                message = await GetSpectrumProfileAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetSpectrumProfile when req is { Value: not null }:
                await SetSpectrumProfileAsync(req.Value).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetSpectrumBrightness:
                message = await GetSpectrumBrightnessAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetSpectrumBrightness when req is { Value: not null }:
                await SetSpectrumBrightnessAsync(req.Value).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetRGBPreset:
                message = await GetRGBPresetAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetRGBPreset when req is { Value: not null }:
                await SetRGBPresetAsync(req.Value).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.IsShellRegistered:
                message = await IsShellRegisteredAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };

            case IpcRequest.OperationType.IsShellInstalled:
                message = IsShellInstalled();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.InstallShell:
                await InstallShellAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.UninstallShell:
                await UninstallShellAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetAppStatus:
                return new IpcResponse { Success = true, Message = BuildAppStatus() };
            case IpcRequest.OperationType.GetNetworkAccelerationStatus:
                return new IpcResponse { Success = true, Message = BuildNetworkAccelerationStatus() };
            case IpcRequest.OperationType.StartNetworkAcceleration:
                return new IpcResponse { Success = true, Message = await StartNetworkAccelerationAsync().ConfigureAwait(false) };
            case IpcRequest.OperationType.StopNetworkAcceleration:
                return new IpcResponse { Success = true, Message = await StopNetworkAccelerationAsync().ConfigureAwait(false) };
            case IpcRequest.OperationType.RunNetworkDiagnostics:
                return new IpcResponse { Success = true, Message = await RunNetworkDiagnosticsAsync().ConfigureAwait(false) };
            default:
                throw new IpcException("Invalid request");
        }
    }

    private string BuildAppStatus()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var repositoryOwner = !string.IsNullOrWhiteSpace(updateCheckSettings.Store.UpdateRepositoryOwner)
            ? updateCheckSettings.Store.UpdateRepositoryOwner
            : AppIdentity.RepositoryOwner;
        var repositoryName = !string.IsNullOrWhiteSpace(updateCheckSettings.Store.UpdateRepositoryName)
            ? updateCheckSettings.Store.UpdateRepositoryName
            : AppIdentity.RepositoryName;
        var updateStatus = updateChecker.Disable
            ? $"disabled ({updateChecker.DisableReason ?? "--disable-update-checker"})"
            : "enabled";

        return string.Join(Environment.NewLine,
            $"{AppIdentity.DisplayName} is running.",
            $"Version: {version}",
            "CLI IPC: enabled",
            $"Update checker: {updateStatus}",
            $"Update repository: {repositoryOwner}/{repositoryName}");
    }

    private string BuildNetworkAccelerationStatus()
    {
        var config = networkAccelerationService.Config;
        return string.Join(Environment.NewLine,
            "Network acceleration status",
            $"State: {networkAccelerationService.StatusText}",
            $"Running: {networkAccelerationService.IsRunning}",
            $"Backend ready: {networkAccelerationService.IsBackendReady}",
            $"Mode: {config.Mode}",
            $"Port: {config.ListenPort}",
            $"Snapshot: {networkStateRecoveryService.SnapshotPath}");
    }

    private async Task<string> StartNetworkAccelerationAsync()
    {
        // Mirror the UI path: enable master switch + a real mode before StartAsync.
        // CLI start is explicit user consent (unlike auto-start on app launch).
        var config = networkAccelerationService.Config;
        var configChanged = false;

        if (!config.AccelerationEnabled || config.Mode is NetworkAccelerationMode.Off)
        {
            config.AccelerationEnabled = true;
            if (config.Mode is NetworkAccelerationMode.Off)
                config.Mode = NetworkAccelerationMode.SystemProxy;
            configChanged = true;
        }

        config.DomainGroups ??= [];
        if (config.DomainGroups.Count == 0)
        {
            config.DomainGroups = BuiltinDomainGroups.CreateDefaults();
            configChanged = true;
        }

        // Prefer selective PAC: enable built-in groups that have domains when none are enabled.
        if (!config.DomainGroups.Any(g => g.Enabled && g.Domains is { Count: > 0 }))
        {
            foreach (var group in config.DomainGroups)
            {
                if ((group.Id is "steam" or "github") &&
                    group.Domains is { Count: > 0 })
                {
                    group.Enabled = true;
                    configChanged = true;
                }
            }

            if (!config.DomainGroups.Any(g => g.Enabled && g.Domains is { Count: > 0 }))
            {
                if (configChanged)
                    await networkAccelerationService.SaveConfigAsync().ConfigureAwait(false);

                return "Network acceleration failed to start: no domain groups with domains are enabled. " +
                       "Enable steam/github (or add custom domains) in the UI, then retry.";
            }
        }

        if (configChanged)
            await networkAccelerationService.SaveConfigAsync().ConfigureAwait(false);

        if (!networkAccelerationService.IsBackendReady)
            return "Network acceleration failed to start: NetworkProxy worker is not available.";

        var started = await networkAccelerationService.StartAsync().ConfigureAwait(false);
        if (!started)
            return "Network acceleration failed to start. System network state was not left enabled.";

        return "Network acceleration started." + Environment.NewLine + BuildNetworkAccelerationStatus();
    }

    private async Task<string> StopNetworkAccelerationAsync()
    {
        await networkAccelerationService.StopAsync().ConfigureAwait(false);
        return BuildNetworkAccelerationStatus();
    }

    private async Task<string> RunNetworkDiagnosticsAsync()
    {
        var report = await networkDiagnosticsService.RunQuickCheckAsync().ConfigureAwait(false);
        return report.Summary;
    }

    private async Task<string> ListQuickActionsAsync()
    {
        var pipelines = await automationProcessor.GetPipelinesAsync().ConfigureAwait(false);
        var quickActions = pipelines
            .Where(p => p.Trigger is null)
            .Select(p => PipelineNameLocalizer.LocalizeStoredName(p.Name) ?? p.Name);

        return string.Join('\n', quickActions);
    }

    private async Task RunQuickActionAsync(string name)
    {
        var pipelines = await automationProcessor.GetPipelinesAsync().ConfigureAwait(false);
        var quickActions = pipelines.Where(p => p.Trigger is null).ToArray();
        var quickAction = quickActions.FirstOrDefault(p =>
                              string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(
                                  PipelineNameLocalizer.LocalizeStoredName(p.Name),
                                  name,
                                  StringComparison.OrdinalIgnoreCase) ||
                              (PipelineNameLocalizer.IsKnownDeactivateGpuTitle(name) &&
                               PipelineNameLocalizer.IsKnownDeactivateGpuTitle(p.Name)))
                          ?? throw new InvalidOperationException($"Quick Action \"{name}\" not found");

        await automationProcessor.RunNowAsync(quickAction.Id).ConfigureAwait(false);
    }

    private static async Task<string?> ListFeaturesAsync(CancellationToken cancellationToken = default)
    {
        if (_supportedFeaturesCache is { } cached)
            return cached;

        await SupportedFeaturesCacheSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_supportedFeaturesCache is { } cachedAfterWait)
                return cachedAfterWait;

            var result = await BuildSupportedFeatureListAsync(FeatureRegistry.All).ConfigureAwait(false);
            if (!result.HasProbeFailures)
                _supportedFeaturesCache = result.FeatureList;

            return result.FeatureList;
        }
        finally
        {
            SupportedFeaturesCacheSemaphore.Release();
        }
    }

    private static async Task<SupportedFeatureProbeResult> GetSupportedFeatureNameAsync(IFeatureRegistration feature)
    {
        try
        {
            var name = await feature.IsSupportedAsync().ConfigureAwait(false)
                ? feature.Name
                : null;

            return new(name, Failed: false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Feature support probe failed. [name={feature.Name}]", ex);

            return new(Name: null, Failed: true);
        }
    }

    private static async Task<SupportedFeatureListResult> BuildSupportedFeatureListAsync(IEnumerable<IFeatureRegistration> registrations)
    {
        var features = await Task.WhenAll(registrations.Select(GetSupportedFeatureNameAsync)).ConfigureAwait(false);
        return new(
            string.Join('\n', features.Select(feature => feature.Name).OfType<string>()),
            features.Any(feature => feature.Failed));
    }

    private readonly record struct SupportedFeatureProbeResult(string? Name, bool Failed);

    private readonly record struct SupportedFeatureListResult(string FeatureList, bool HasProbeFailures);

    private static async Task<string?> ListFeatureValuesAsync(string name)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        var values = await feature.GetValuesAsync().ConfigureAwait(false);
        return string.Join('\n', values);
    }

    private static async Task<string> GetFeatureValueAsync(string name)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        return await feature.GetValueAsync().ConfigureAwait(false);
    }

    private static async Task SetFeatureValueAsync(string name, string value)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        await feature.SetValueAsync(value).ConfigureAwait(false);
    }

    private async Task<string> GetSpectrumProfileAsync()
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        var profile = await spectrumKeyboardBacklightController.GetProfileAsync().ConfigureAwait(false);
        return $"{profile}";
    }

    private async Task SetSpectrumProfileAsync(string value)
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        await spectrumKeyboardBacklightController.SetProfileAsync(Convert.ToInt32(value)).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    private async Task<string> GetSpectrumBrightnessAsync()
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        var profile = await spectrumKeyboardBacklightController.GetBrightnessAsync().ConfigureAwait(false);
        return $"{profile}";
    }

    private async Task SetSpectrumBrightnessAsync(string value)
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        await spectrumKeyboardBacklightController.SetBrightnessAsync(Convert.ToInt32(value)).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    private async Task<string> GetRGBPresetAsync()
    {
        if (!await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("RGB is not supported");

        var state = await rgbKeyboardBacklightController.GetStateAsync().ConfigureAwait(false);
        return $"{(int)state.SelectedPreset + 1}";
    }

    private async Task SetRGBPresetAsync(string value)
    {
        if (!await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("RGB is not supported");

        var preset = (RGBKeyboardBacklightPreset)(Convert.ToInt32(value) - 1);

        if (!Enum.IsDefined(preset))
            throw new InvalidOperationException("Invalid preset");

        await rgbKeyboardBacklightController.SetLightControlOwnerAsync(true).ConfigureAwait(false);

        await rgbKeyboardBacklightController.SetPresetAsync(preset).ConfigureAwait(false);



        MessagingCenter.Publish(new RGBKeyboardBacklightChangedMessage());

    }



    private static Task<string> IsShellRegisteredAsync()
    {
        // Shell integration (and the plugin system that provided it) has been
        // retired; the shell registration state is no longer managed here.
        return Task.FromResult("false");
    }




    private static string IsShellInstalled()
    {
        // Shell integration was retired with the plugin system. Use the OS directly.
        return "false";
    }

    private static Task InstallShellAsync()
    {
        // Shell integration was retired with the plugin system.
        return Task.FromException(new IpcException("Shell integration is no longer available."));
    }

    private static Task UninstallShellAsync()
    {
        // Shell integration was retired with the plugin system.
        return Task.FromException(new IpcException("Shell integration is no longer available."));
    }
}
