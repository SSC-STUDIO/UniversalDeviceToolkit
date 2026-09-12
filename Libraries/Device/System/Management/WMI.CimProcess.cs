using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    private static readonly TimeSpan GameZoneCimWriteTimeout = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan GameZoneCimCleanupTimeout = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Invokes a Lenovo GameZone WMI method through a short-lived powershell.exe running
    /// CIM cmdlets (Microsoft.Management.Infrastructure channel). Some Legion WMI providers
    /// return empty out-parameters to classic System.Management clients (probed on
    /// Y9000P IRX9: ReturnValue missing, Data null); the MMI channel used by the CIM
    /// cmdlets marshals them correctly. Returns the method's integer "Data" out-parameter,
    /// or 0 when unavailable.
    /// </summary>
    internal static Task<int> InvokeGameZoneMethodViaCimProcessAsync(
        string methodName,
        int? dataParam = null,
        CancellationToken cancellationToken = default) =>
        InvokeGameZoneMethodViaCimProcessAsync(methodName, dataParam, "Data", cancellationToken);

    internal static async Task<int> InvokeGameZoneMethodViaCimProcessAsync(
        string methodName,
        int? dataParam,
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeCimIdentifier(methodName) || !IsSafeCimIdentifier(parameterName))
            return 0;

        var arguments = dataParam.HasValue ? $" -Arguments @{{{parameterName}={dataParam.Value}}}" : string.Empty;
        var script = string.Concat(
            "$i=@(Get-CimInstance -Namespace root\\WMI -ClassName LENOVO_GAMEZONE_DATA -ErrorAction Stop);",
            "if($i.Count -eq 0){exit 2};",
            $"$r=Invoke-CimMethod -InputObject $i[0] -MethodName {methodName}{arguments} -ErrorAction Stop;",
            "if($null -ne $r.Data){[int]$r.Data}");

        var result = await InvokeGameZoneCimProcessCoreAsync(
            methodName,
            script,
            cancellationToken).ConfigureAwait(false);
        return result.Success && int.TryParse(result.Output, out var value) ? value : 0;
    }

    /// <summary>
    /// Write-only CIM process path. A zero/empty method output is still successful when
    /// Invoke-CimMethod completes and the process emits the explicit completion sentinel.
    /// Production callers must invoke this primitive inside CallWriteSequenceAsync.
    /// </summary>
    internal static Task<WmiWriteResult> InvokeGameZoneWriteViaCimProcessAsync(
        string methodName,
        int value,
        string parameterName = "Data",
        CancellationToken cancellationToken = default) =>
        InvokeGameZoneWriteViaCimProcessAsync(
            methodName,
            value,
            parameterName,
            GameZoneCimWriteTimeout,
            startInfo => new SystemGameZoneCimProcess(startInfo),
            Task.Delay,
            cancellationToken);

    internal static async Task<WmiWriteResult> InvokeGameZoneWriteViaCimProcessAsync(
        string methodName,
        int value,
        string parameterName,
        TimeSpan timeout,
        Func<ProcessStartInfo, IGameZoneCimProcess> processFactory,
        Func<TimeSpan, Task> timeoutTaskFactory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeCimIdentifier(methodName) || !IsSafeCimIdentifier(parameterName))
            return WmiWriteResult.Unavailable;
        ArgumentNullException.ThrowIfNull(processFactory);
        ArgumentNullException.ThrowIfNull(timeoutTaskFactory);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var script = string.Concat(
            "$i=@(Get-CimInstance -Namespace root\\WMI -ClassName LENOVO_GAMEZONE_DATA -ErrorAction Stop);",
            "if($i.Count -eq 0){exit 2};",
            $"$null=Invoke-CimMethod -InputObject $i[0] -MethodName {methodName} " +
            $"-Arguments @{{{parameterName}={value}}} -ErrorAction Stop;",
            "[Console]::Out.Write('UDT_WRITE_OK')");
        var startInfo = CreateGameZoneCimProcessStartInfo(script);
        using var process = processFactory(startInfo);
        var launched = false;
        Task? waitTask = null;
        Task<string>? outputTask = null;
        Task<string>? errorTask = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.Start();
            launched = true;

            waitTask = process.WaitForExitAsync();
            outputTask = process.ReadStandardOutputToEndAsync();
            errorTask = process.ReadStandardErrorToEndAsync();
            var timeoutTask = timeoutTaskFactory(timeout)
                ?? throw new InvalidOperationException("The CIM write timeout task factory returned null.");
            var cancellationTask = cancellationToken.CanBeCanceled
                ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                : Task.Delay(Timeout.InfiniteTimeSpan);

            var completedTask = await Task.WhenAny(
                waitTask,
                timeoutTask,
                cancellationTask).ConfigureAwait(false);
            if (completedTask == waitTask || waitTask.IsCompleted)
            {
                await waitTask.ConfigureAwait(false);
                var output = (await outputTask.ConfigureAwait(false)).Trim();
                var error = (await errorTask.ConfigureAwait(false)).Trim();
                if (process.ExitCode == 2)
                    return WmiWriteResult.Unavailable;
                if (process.ExitCode != 0)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace(
                            $"CIM write process failed (exit {process.ExitCode}): {error} [method={methodName}]");
                    return WmiWriteResult.FailedIndeterminate;
                }

                return ClassifyGameZoneCimWriteResult(true, output);
            }

            await TerminateGameZoneCimProcessAsync(
                process,
                waitTask,
                outputTask,
                errorTask,
                methodName,
                timeoutTaskFactory).ConfigureAwait(false);
            return completedTask == timeoutTask
                ? WmiWriteResult.TimedOutIndeterminate
                : WmiWriteResult.FailedIndeterminate;
        }
        catch (OperationCanceledException) when (!launched && cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!launched)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"CIM write process failed before launch [method={methodName}]", ex);
                return WmiWriteResult.Unavailable;
            }

            await TerminateGameZoneCimProcessAsync(
                process,
                waitTask,
                outputTask,
                errorTask,
                methodName,
                timeoutTaskFactory).ConfigureAwait(false);
            Log.Instance.Warning(
                $"CIM write process failed after launch; side effect is indeterminate. [method={methodName}]",
                ex);
            return WmiWriteResult.FailedIndeterminate;
        }
    }

    internal static WmiWriteResult ClassifyGameZoneCimWriteResult(
        bool processSucceeded,
        string output) =>
        processSucceeded && string.Equals(output, "UDT_WRITE_OK", StringComparison.Ordinal)
            ? WmiWriteResult.Success
            : WmiWriteResult.FailedIndeterminate;

    private static ProcessStartInfo CreateGameZoneCimProcessStartInfo(string script) =>
        new()
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

    private static async Task TerminateGameZoneCimProcessAsync(
        IGameZoneCimProcess process,
        Task? waitTask,
        Task<string>? outputTask,
        Task<string>? errorTask,
        string methodName,
        Func<TimeSpan, Task> cleanupTaskFactory)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Failed to terminate timed-out CIM write process. [method={methodName}]", ex);
        }

        if (waitTask is null)
        {
            Log.Instance.Warning(
                $"CIM write process exit could not be observed; retaining write ownership. [method={methodName}]");
            await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            return;
        }

        var cleanupCompleted = await Task.WhenAny(
            waitTask,
            cleanupTaskFactory(GameZoneCimCleanupTimeout)).ConfigureAwait(false);
        if (cleanupCompleted != waitTask && !waitTask.IsCompleted)
        {
            Log.Instance.Warning($"Timed-out CIM write process did not exit after termination. [method={methodName}]");
        }

        try
        {
            await waitTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning(
                $"CIM write process exit observation failed; retaining write ownership. [method={methodName}]",
                ex);
            await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            return;
        }

        try
        {
            if (outputTask is not null)
                _ = await outputTask.ConfigureAwait(false);
            if (errorTask is not null)
                _ = await errorTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"CIM write process cleanup failed. [method={methodName}]", ex);
        }
    }

    private static async Task<CimProcessResult> InvokeGameZoneCimProcessCoreAsync(
        string methodName,
        string script,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = (await outputTask.ConfigureAwait(false)).Trim();

            if (process.ExitCode != 0)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"CIM process call failed (exit {process.ExitCode}): {(await errorTask.ConfigureAwait(false)).Trim()} [method={methodName}]");
                return CimProcessResult.Failed;
            }

            return new CimProcessResult(true, output);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"CIM process call failed [method={methodName}]", ex);
            return CimProcessResult.Failed;
        }
    }

    private readonly record struct CimProcessResult(bool Success, string Output)
    {
        internal static CimProcessResult Failed => new(false, string.Empty);
    }

    private static bool IsSafeCimIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64)
            return false;
        if (value[0] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z') and not '_')
            return false;
        foreach (var ch in value)
        {
            if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')
                continue;
            return false;
        }

        return true;
    }
}

internal interface IGameZoneCimProcess : IDisposable
{
    bool HasExited { get; }
    int ExitCode { get; }

    void Start();
    void Kill(bool entireProcessTree);
    Task WaitForExitAsync();
    Task<string> ReadStandardOutputToEndAsync();
    Task<string> ReadStandardErrorToEndAsync();
}

internal sealed class SystemGameZoneCimProcess : IGameZoneCimProcess
{
    private readonly Process _process;

    internal SystemGameZoneCimProcess(ProcessStartInfo startInfo)
    {
        _process = new Process { StartInfo = startInfo };
    }

    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.ExitCode;

    public void Start() => _process.Start();
    public void Kill(bool entireProcessTree) => _process.Kill(entireProcessTree);
    public Task WaitForExitAsync() => _process.WaitForExitAsync();
    public Task<string> ReadStandardOutputToEndAsync() => _process.StandardOutput.ReadToEndAsync();
    public Task<string> ReadStandardErrorToEndAsync() => _process.StandardError.ReadToEndAsync();
    public void Dispose() => _process.Dispose();
}
