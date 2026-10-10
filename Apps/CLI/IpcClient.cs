using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.CLI.Lib;
using UniversalDeviceToolkit.CLI.Lib.Extensions;

namespace UniversalDeviceToolkit.CLI;

public static class IpcClient
{
    /// <summary>When true, skip the long legacy-pipe retry so --json callers fail fast.</summary>
    public static bool FastFail { get; set; }


    public static async Task<string> GetAppStatusAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetAppStatus
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static async Task<string> ListQuickActionsAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.ListQuickActions
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static Task RunQuickActionAsync(string name)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.QuickAction,
            Name = name
        };

        return SendRequestAsync(req);
    }

    public static async Task<string> ListFeaturesAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.ListFeatures,
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static async Task<string> ListFeatureValuesAsync(string name)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.ListFeatureValues,
            Name = name,
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static Task SetFeatureValueAsync(string name, string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetFeatureValue,
            Name = name,
            Value = value
        };

        return SendRequestAsync(req);
    }

    public static async Task<string> GetFeatureValueAsync(string name)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetFeatureValue,
            Name = name
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static async Task<string> GetSpectrumProfileAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetSpectrumProfile
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static Task SetSpectrumProfileAsync(string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetSpectrumProfile,
            Value = value
        };

        return SendRequestAsync(req);
    }

    public static async Task<string> GetSpectrumBrightnessAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetSpectrumBrightness
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static Task SetSpectrumBrightnessAsync(string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetSpectrumBrightness,
            Value = value
        };

        return SendRequestAsync(req);
    }

    public static async Task<string> GetRGBPresetAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetRGBPreset
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static Task SetRGBPresetAsync(string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetRGBPreset,
            Value = value
        };

        return SendRequestAsync(req);
    }

    public static async Task<bool> IsShellRegisteredAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.IsShellRegistered
        };

        var result = await SendRequestAsync(req).ConfigureAwait(false);
        return result?.ToLowerInvariant() == "true";
    }

    public static async Task<bool> IsShellInstalledAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.IsShellInstalled
        };

        var result = await SendRequestAsync(req).ConfigureAwait(false);
        return result?.ToLowerInvariant() == "true";
    }

    public static Task InstallShellAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.InstallShell
        };

        return SendRequestAsync(req);
    }

    public static Task UninstallShellAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.UninstallShell
        };

        return SendRequestAsync(req);
    }

    public static async Task<string> GetNetworkAccelerationStatusAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetNetworkAccelerationStatus
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static async Task<string> StartNetworkAccelerationAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.StartNetworkAcceleration
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static async Task<string> StopNetworkAccelerationAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.StopNetworkAcceleration
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    public static async Task<string> RunNetworkDiagnosticsAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.RunNetworkDiagnostics
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException(Strings.Get("CLI_IpcError_MissingReturnMessage", "Missing return message"));
    }

    private static async Task<string?> SendRequestAsync(IpcRequest req)
    {
        using var loading = ConsoleLoadingAnimation.Start(GetLoadingMessage(req), enabled: !FastFail);
        using var pipe = await ConnectToAvailablePipeAsync().ConfigureAwait(false);

        var challengeResponse = await pipe.ReadObjectAsync<IpcResponse>().ConfigureAwait(false);
        if (challengeResponse is null || !challengeResponse.Success || challengeResponse.Message is null)
            throw new IpcException(Strings.Get("CLI_IpcError_AuthChallengeFailed", "Failed to receive authentication challenge"));

        var challenge = Convert.FromHexString(challengeResponse.Message);
        req.AuthToken = ComputeAuthToken(challenge);

        await pipe.WriteObjectAsync(req).ConfigureAwait(false);
        var res = await pipe.ReadObjectAsync<IpcResponse>().ConfigureAwait(false);

        if (res is null || !res.Success)
            throw new IpcException(res?.Message ?? Strings.Get("CLI_IpcError_UnknownFailure", "Unknown failure"));

        return res.Message;
    }

    /// <summary>
    /// Try preferred UDT pipe first (short timeout), then fall back to legacy DEFAULT.
    /// </summary>
    private static async Task<NamedPipeClientStream> ConnectToAvailablePipeAsync()
    {
        var pipeNames = GetClientPipeNames();
        Exception? lastError = null;

        for (var i = 0; i < pipeNames.Length; i++)
        {
            var pipeName = pipeNames[i];
            var isFallback = i > 0;
            var attempts = isFallback
                ? (FastFail ? PreferredConnectAttempts : ConnectMaxAttempts)
                : PreferredConnectAttempts;
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

            try
            {
                await ConnectAsync(pipe, attempts).ConfigureAwait(false);
                if (isFallback)
                    Trace.WriteLine($"CLI IPC: preferred pipe unavailable; fell back to '{pipeName}'.");
                return pipe;
            }
            catch (Exception ex) when (ex is TimeoutException or IpcConnectException or IOException)
            {
                lastError = ex;
                await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (lastError is IpcConnectException ipc)
            throw ipc;

        throw new IpcConnectException();
    }

    private static string ComputeAuthToken(byte[] challenge) => Convert.ToHexString(challenge);

    private static string GetLoadingMessage(IpcRequest req)
    {
        var (key, fallback) = req.Operation switch
        {
            IpcRequest.OperationType.ListFeatures =>
                ("CLI_Loading_ListFeatures", "Loading features"),
            IpcRequest.OperationType.ListFeatureValues =>
                ("CLI_Loading_ListFeatureValues", "Loading values"),
            IpcRequest.OperationType.ListQuickActions =>
                ("CLI_Loading_ListQuickActions", "Loading quick actions"),
            IpcRequest.OperationType.GetFeatureValue =>
                ("CLI_Loading_GetFeatureValue", "Reading feature"),
            IpcRequest.OperationType.SetFeatureValue =>
                ("CLI_Loading_SetFeatureValue", "Applying feature"),
            IpcRequest.OperationType.GetSpectrumProfile =>
                ("CLI_Loading_GetSpectrumProfile", "Reading Spectrum profile"),
            IpcRequest.OperationType.SetSpectrumProfile =>
                ("CLI_Loading_SetSpectrumProfile", "Applying Spectrum profile"),
            IpcRequest.OperationType.GetSpectrumBrightness =>
                ("CLI_Loading_GetSpectrumBrightness", "Reading Spectrum brightness"),
            IpcRequest.OperationType.SetSpectrumBrightness =>
                ("CLI_Loading_SetSpectrumBrightness", "Applying Spectrum brightness"),
            IpcRequest.OperationType.GetRGBPreset =>
                ("CLI_Loading_GetRGBPreset", "Reading RGB preset"),
            IpcRequest.OperationType.SetRGBPreset =>
                ("CLI_Loading_SetRGBPreset", "Applying RGB preset"),
            IpcRequest.OperationType.QuickAction =>
                ("CLI_Loading_QuickAction", "Running quick action"),
            IpcRequest.OperationType.IsShellRegistered =>
                ("CLI_Loading_IsShellRegistered", "Checking shell registration"),
            IpcRequest.OperationType.IsShellInstalled =>
                ("CLI_Loading_IsShellInstalled", "Checking shell installation"),
            IpcRequest.OperationType.InstallShell =>
                ("CLI_Loading_InstallShell", "Starting shell installation"),
            IpcRequest.OperationType.UninstallShell =>
                ("CLI_Loading_UninstallShell", "Starting shell uninstallation"),
            IpcRequest.OperationType.GetAppStatus =>
                ("CLI_Loading_GetAppStatus", "Checking app status"),
            IpcRequest.OperationType.GetNetworkAccelerationStatus =>
                ("CLI_Loading_GetNetworkAccelerationStatus", "Checking network acceleration"),
            IpcRequest.OperationType.StartNetworkAcceleration =>
                ("CLI_Loading_StartNetworkAcceleration", "Starting network acceleration"),
            IpcRequest.OperationType.StopNetworkAcceleration =>
                ("CLI_Loading_StopNetworkAcceleration", "Stopping network acceleration"),
            IpcRequest.OperationType.RunNetworkDiagnostics =>
                ("CLI_Loading_RunNetworkDiagnostics", "Running network diagnostics"),
            _ =>
                ("CLI_Loading_Default", "Waiting for Universal Device Toolkit")
        };

        return $"{Strings.Get(key, fallback)}{FormatTarget(req.Name)}";
    }

    private static string FormatTarget(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : $" '{value}'";

    private const int ConnectMaxAttempts = 40;

    /// <summary>
    /// Short probe budget for the preferred UDT pipe before falling back to legacy.
    /// </summary>
    private const int PreferredConnectAttempts = 2;

    private static async Task ConnectAsync(NamedPipeClientStream pipe, int maxAttempts = ConnectMaxAttempts)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await pipe.ConnectAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None).ConfigureAwait(false);
                pipe.ReadMode = PipeTransmissionMode.Message;
                return;
            }
            catch (TimeoutException)
            {
                if (attempt < maxAttempts - 1)
                {
                    var baseDelay = (int)Math.Min(200 * Math.Pow(2, attempt), 3000);
                    var jitter = Random.Shared.Next(-50, 51);
                    var delayMs = Math.Max(0, baseDelay + jitter);
                    await Task.Delay(delayMs, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        throw new IpcConnectException();
    }

    private static string[] GetClientPipeNames()
        => Constants.GetClientPipeNamesFromEnvironment();
}
