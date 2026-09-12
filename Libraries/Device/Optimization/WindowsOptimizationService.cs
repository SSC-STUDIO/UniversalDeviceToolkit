using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using ToolkitRegistry = UniversalDeviceToolkit.Lib.System.Registry;

namespace UniversalDeviceToolkit.Lib.Optimization;

public record WindowsOptimizationActionDefinition(
    string Key,
    string TitleResourceKey,
    string DescriptionResourceKey,
    Func<CancellationToken, Task> ExecuteAsync,
    bool Recommended = true,
    Func<CancellationToken, Task<bool>>? IsAppliedAsync = null,
    Type? ResourceAnchorType = null,
    Func<CancellationToken, Task>? RollbackAsync = null)
{
    public WindowsOptimizationActionDefinition(
        string key,
        string titleResourceKey,
        string descriptionResourceKey,
        Func<CancellationToken, Task> executeAsync,
        bool recommended,
        Func<CancellationToken, Task<bool>>? isAppliedAsync)
        : this(key, titleResourceKey, descriptionResourceKey, executeAsync, recommended, isAppliedAsync, null)
    {
    }
}

public record WindowsOptimizationCategoryDefinition(
    string Key,
    string TitleResourceKey,
    string DescriptionResourceKey,
    IReadOnlyList<WindowsOptimizationActionDefinition> Actions,
    Type? ResourceAnchorType = null)
{
    public WindowsOptimizationCategoryDefinition(
        string key,
        string titleResourceKey,
        string descriptionResourceKey,
        IReadOnlyList<WindowsOptimizationActionDefinition> actions)
        : this(key, titleResourceKey, descriptionResourceKey, actions, null)
    {
    }
}

/// <summary>
/// Service for executing Windows optimization commands with strict security validation.
/// Prevents command injection attacks through whitelist-based command validation.
/// </summary>
public class WindowsOptimizationService
{
    public const string CleanupCategoryKey = "cleanup";
    public const string CustomCleanupActionKey = "cleanup.custom";

    // Whitelist of allowed executables for command execution
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "powercfg",      // Power configuration
        "ipconfig",      // Network configuration
        "netsh",         // Network shell
        "dism",          // Deployment Image Servicing and Management
        "del",           // Delete files (restricted)
        "rd",            // Remove directory (restricted)
        "cmd.exe",       // Command prompt (with validation)
        "reg",           // Registry operations
        "schtasks",      // Task scheduler
        "sc",            // Service control
        "wevtutil",      // Windows Event Utility
        "cleanmgr",      // Disk cleanup
    };

    // Commands that require special argument validation
    private static readonly HashSet<string> HighRiskCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "del",
        "rd",
        "cmd.exe",
        "reg"
    };

    private const string HighPerformancePowerSchemeGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private readonly WindowsCleanupService _cleanupService;
    private readonly WindowsOptimizationCategoryProvider _categoryProvider;
    private readonly object _rollbackStateLock = new();
    private readonly Dictionary<string, IReadOnlyList<RegistryOriginalValue>> _registryOriginalValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<ServiceOriginalValue>> _serviceOriginalValues = new(StringComparer.OrdinalIgnoreCase);

    private sealed record RegistryOriginalValue(RegistryValueDefinition Tweak, bool Existed, object? Value);
    private sealed record ServiceOriginalValue(string ServiceName, int StartValue);

    public WindowsOptimizationService(WindowsCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
        _categoryProvider = new WindowsOptimizationCategoryProvider(this, cleanupService);
    }

    private IReadOnlyDictionary<string, WindowsOptimizationActionDefinition> GetActionsByKey()
    {
        return GetCategories()
            .SelectMany(category => category.Actions)
            .GroupBy(action => action.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WindowsOptimizationCategoryDefinition> GetCategories()
        => new List<WindowsOptimizationCategoryDefinition>(_categoryProvider.BuildCategories());

    public async Task ApplyActionAsync(string actionKey, CancellationToken cancellationToken)
    {
        // Validate action key to prevent injection
        if (!IsValidActionKey(actionKey))
            throw ExceptionHelper.InvalidActionKey(nameof(actionKey));

        var actions = GetActionsByKey();
        if (!actions.TryGetValue(actionKey, out var action))
            throw ExceptionHelper.OptimizationActionNotFound(actionKey);

        await action.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RevertActionAsync(string actionKey, CancellationToken cancellationToken)
    {
        if (!IsValidActionKey(actionKey))
            throw ExceptionHelper.InvalidActionKey(nameof(actionKey));

        var actions = GetActionsByKey();
        if (!actions.TryGetValue(actionKey, out var action))
            throw ExceptionHelper.OptimizationActionNotFound(actionKey);

        if (action.RollbackAsync is null)
            throw ExceptionHelper.OptimizationActionRollbackUnavailable(actionKey);

        await action.RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsActionAppliedAsync(string actionKey, CancellationToken cancellationToken)
    {
        // Validate action key to prevent injection
        if (!IsValidActionKey(actionKey))
            throw ExceptionHelper.InvalidActionKey(nameof(actionKey));

        var actions = GetActionsByKey();
        if (actions.TryGetValue(actionKey, out var action))
        {
            if (action.IsAppliedAsync is not null)
            {
                return await action.IsAppliedAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public async Task ExecuteActionsAsync(IEnumerable<string> actionKeys, CancellationToken cancellationToken)
    {
        if (actionKeys is null)
            return;

        var actionsByKey = GetActionsByKey();
        var appliedActions = new List<(string key, WindowsOptimizationActionDefinition action)>();

        try
        {
            foreach (var key in actionKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // Validate each action key
                if (!IsValidActionKey(key))
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Skipping invalid action key: {key}");
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!actionsByKey.TryGetValue(key, out var action))
                    continue;

                await action.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                appliedActions.Add((key, action));
            }
        }
        catch (Exception)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Action execution failed, rolling back {appliedActions.Count} applied action(s).");

            // Rollback in reverse order
            for (int i = appliedActions.Count - 1; i >= 0; i--)
            {
                var (actionKey, actionDef) = appliedActions[i];
                try
                {
                    if (actionDef.RollbackAsync is not null)
                        await actionDef.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception rollbackEx)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Rollback failed for action. [key={actionKey}]", rollbackEx);
                }
            }

            throw;
        }
    }

    public Task ApplyPerformanceOptimizationsAsync(CancellationToken cancellationToken)
    {
        var keys = GetCategories()
            .Where(category => !string.Equals(category.Key, CleanupCategoryKey, StringComparison.OrdinalIgnoreCase))
            .SelectMany(category => category.Actions.Where(action => action.Recommended).Select(action => action.Key));

        return ExecuteActionsAsync(keys, cancellationToken);
    }

    public Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        var keys = GetCategories()
            .Where(category => category.Key.StartsWith("cleanup.", StringComparison.OrdinalIgnoreCase))
            .SelectMany(category => category.Actions.Where(action => action.Recommended).Select(action => action.Key));

        return ExecuteActionsAsync(keys, cancellationToken);
    }

    public Task<long> EstimateCleanupSizeAsync(IEnumerable<string> actionKeys, CancellationToken cancellationToken)
    {
        return _cleanupService.EstimateCleanupSizeAsync(actionKeys, cancellationToken);
    }

    public Task<long> EstimateActionSizeAsync(string actionKey, CancellationToken cancellationToken)
    {
        return _cleanupService.EstimateActionSizeAsync(actionKey, cancellationToken);
    }

    public Task<List<FileInfo>> GetLargeFilesAsync(long minSize, CancellationToken cancellationToken)
    {
        return _cleanupService.GetLargeFilesAsync(minSize, cancellationToken);
    }

    public async Task<bool?> TryGetActionAppliedAsync(string actionKey, CancellationToken cancellationToken)
    {
        // Validate action key
        if (!IsValidActionKey(actionKey))
            throw ExceptionHelper.InvalidActionKey(nameof(actionKey));

        var actionsByKey = GetActionsByKey();
        if (!actionsByKey.TryGetValue(actionKey, out var definition))
            return null;

        if (definition.IsAppliedAsync is null)
            return null;

        try
        {
            return await definition.IsAppliedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation means the page no longer needs this probe. Preserve it so
            // the caller cannot publish a partially refreshed state as authoritative.
            throw;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to evaluate optimization action state. [action={actionKey}]", ex);
            return null;
        }
    }

    internal WindowsOptimizationActionDefinition CreateRegistryAction(
        string key,
        string titleResourceKey,
        string descriptionResourceKey,
        IReadOnlyList<RegistryValueDefinition> tweaks,
        bool recommended = true)
    {
        return new(
            key,
            titleResourceKey,
            descriptionResourceKey,
            async ct =>
            {
                CaptureRegistryOriginalValuesIfNeeded(key, tweaks);
                await ApplyRegistryTweaksAsync(ct, tweaks).ConfigureAwait(false);
            },
            recommended,
            ct => Task.FromResult(WindowsOptimizationHelper.AreRegistryTweaksApplied(tweaks)),
            RollbackAsync: ct => RevertRegistryActionAsync(key, tweaks, ct));
    }

    internal WindowsOptimizationActionDefinition CreateServiceAction(
        string key,
        string titleResourceKey,
        string descriptionResourceKey,
        IReadOnlyList<string> services,
        bool recommended = true)
    {
        return new(
            key,
            titleResourceKey,
            descriptionResourceKey,
            async ct =>
            {
                CaptureServiceOriginalValuesIfNeeded(key, services);
                await DisableServicesAsync(ct, services).ConfigureAwait(false);
            },
            recommended,
            ct => Task.FromResult(WindowsOptimizationHelper.AreServicesDisabled(services)),
            RollbackAsync: ct => RevertServiceActionAsync(key, services, ct));
    }

    /// <summary>
    /// Creates a command action with strict validation.
    /// Commands are validated against an allowlist before execution.
    /// </summary>
    internal WindowsOptimizationActionDefinition CreateCommandAction(
        string key,
        string titleResourceKey,
        string descriptionResourceKey,
        IReadOnlyList<string> commands,
        bool recommended = true,
        Func<CancellationToken, Task<bool>>? isAppliedAsync = null,
        Func<CancellationToken, Task>? rollbackAsync = null)
    {
        // Validate all commands at creation time
        foreach (var command in commands)
        {
            if (!IsValidCommand(command))
                throw new ArgumentException(string.Format(Resource.Exception_CommandFailedSecurity, command), nameof(commands));
        }

        return new(
            key,
            titleResourceKey,
            descriptionResourceKey,
            ct => ExecuteCommandsSequentiallyAsync(ct, commands.ToArray()),
            recommended,
            isAppliedAsync,
            RollbackAsync: rollbackAsync ?? (static ct => Task.CompletedTask));
    }

    private void CaptureRegistryOriginalValuesIfNeeded(string actionKey, IReadOnlyList<RegistryValueDefinition> tweaks)
    {
        lock (_rollbackStateLock)
        {
            if (_registryOriginalValues.ContainsKey(actionKey) || WindowsOptimizationHelper.AreRegistryTweaksApplied(tweaks))
                return;

            _registryOriginalValues[actionKey] = tweaks
                .Select(tweak => new RegistryOriginalValue(
                    tweak,
                    ToolkitRegistry.ValueExists(tweak.Hive, tweak.SubKey, tweak.ValueName),
                    ToolkitRegistry.GetValue<object?>(tweak.Hive, tweak.SubKey, tweak.ValueName, null)))
                .ToArray();
        }
    }

    private Task RevertRegistryActionAsync(
        string actionKey,
        IReadOnlyList<RegistryValueDefinition> tweaks,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RegistryOriginalValue>? originals;
        lock (_rollbackStateLock)
            _registryOriginalValues.TryGetValue(actionKey, out originals);

        foreach (var tweak in tweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var original = originals?.FirstOrDefault(value => value.Tweak.Equals(tweak));
            if (original is not null && original.Existed)
            {
                ToolkitRegistry.SetValue(tweak.Hive, tweak.SubKey, tweak.ValueName, original.Value!, true, tweak.Kind);
            }
            else
            {
                // A previous application may predate the snapshot store. Removing the
                // optimization value restores Windows' inherited/default behavior.
                ToolkitRegistry.DeleteValue(tweak.Hive, tweak.SubKey, tweak.ValueName, true);
            }
        }

        lock (_rollbackStateLock)
            _registryOriginalValues.Remove(actionKey);

        return Task.CompletedTask;
    }

    private void CaptureServiceOriginalValuesIfNeeded(string actionKey, IReadOnlyList<string> services)
    {
        lock (_rollbackStateLock)
        {
            if (_serviceOriginalValues.ContainsKey(actionKey) || WindowsOptimizationHelper.AreServicesDisabled(services))
                return;

            _serviceOriginalValues[actionKey] = services
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(service => new ServiceOriginalValue(
                    service,
                    ToolkitRegistry.GetValue<int>(
                        "HKEY_LOCAL_MACHINE",
                        $@"SYSTEM\CurrentControlSet\Services\{service}", "Start", -1)))
                .ToArray();
        }
    }

    private Task RevertServiceActionAsync(
        string actionKey,
        IReadOnlyList<string> services,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ServiceOriginalValue>? originals;
        lock (_rollbackStateLock)
            _serviceOriginalValues.TryGetValue(actionKey, out originals);

        foreach (var service in services.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var original = originals?.FirstOrDefault(value =>
                string.Equals(value.ServiceName, service, StringComparison.OrdinalIgnoreCase));
            var startValue = original?.StartValue ?? 3;
            if (startValue < 0 && !ToolkitRegistry.ValueExists(
                    "HKEY_LOCAL_MACHINE",
                    $@"SYSTEM\CurrentControlSet\Services\{service}",
                    "Start"))
            {
                continue;
            }

            ToolkitRegistry.SetValue(
                "HKEY_LOCAL_MACHINE",
                $@"SYSTEM\CurrentControlSet\Services\{service}",
                "Start", startValue, true, RegistryValueKind.DWord);
        }

        lock (_rollbackStateLock)
            _serviceOriginalValues.Remove(actionKey);

        return Task.CompletedTask;
    }

    private Task ApplyRegistryTweaksAsync(CancellationToken cancellationToken, IEnumerable<RegistryValueDefinition> tweaks)
    {
        foreach (var tweak in tweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsOptimizationHelper.ApplyRegistryTweak(tweak);
        }

        return Task.CompletedTask;
    }

    private Task DisableServicesAsync(CancellationToken cancellationToken, IEnumerable<string> services)
    {
        foreach (var serviceName in services.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Validate service name
            if (!IsValidServiceName(serviceName))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Skipping invalid service name: {serviceName}");
                continue;
            }
            
            WindowsOptimizationHelper.DisableService(serviceName);
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteCommandsSequentiallyAsync(CancellationToken cancellationToken, params string[] commands)
    {
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Validate command before execution
            if (!IsValidCommand(command))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Skipping invalid command: {command}");
                continue;
            }
            
            await ExecuteCommandLineAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes a command line with strict security validation.
    /// Uses parameterized execution instead of string concatenation.
    /// </summary>
    private async Task ExecuteCommandLineAsync(string command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw ExceptionHelper.CommandCannotBeEmpty(nameof(command));

        // Validate command before execution
        if (!IsValidCommand(command))
        {
            throw ExceptionHelper.CommandFailedSecurity(command);
        }

        try
        {
            // Parse command using proper argument parsing instead of simple split
            var (fileName, arguments) = ParseCommandLine(command);
            
            // Double-check the parsed command
            if (!IsAllowedExecutable(fileName))
            {
                throw ExceptionHelper.NotInAllowlist(fileName);
            }

            // Build process start info with parameterized arguments
            var startInfo = BuildProcessStartInfo(fileName, arguments, command);

            Process? process = null;
            try
            {
                process = new Process { StartInfo = startInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                await Task.WhenAll(process.WaitForExitAsync(cancellationToken), outputTask, errorTask).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    var errorOutput = (await errorTask.ConfigureAwait(false)).Trim();
                    throw ExceptionHelper.CommandExitedNonZero(fileName, process.ExitCode, errorOutput);
                }
            }
            finally
            {
                if (process is not null)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Instance.TraceOnce(
                            "opt-cmd-kill",
                            $"Failed to kill optimization command process ({fileName}); process may already have exited.",
                            ex);
                    }
                    process.Dispose();
                }
            }

            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace($"Command executed successfully: {fileName}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to execute command. [command={command}]", ex);
            throw;
        }
    }

    /// <summary>
    /// Parses a command line string into filename and arguments.
    /// Handles quoted paths correctly.
    /// </summary>
    private static (string fileName, string arguments) ParseCommandLine(string command)
    {
        command = command.Trim();
        
        string fileName;
        string arguments;

        if (command.StartsWith("\"", StringComparison.Ordinal))
        {
            // Quoted path
            var endQuote = command.IndexOf('\"', 1);
            if (endQuote == -1)
            {
                // No closing quote, treat entire command as filename
                fileName = command.Trim('\"');
                arguments = string.Empty;
            }
            else
            {
                fileName = command.Substring(1, endQuote - 1);
                arguments = command.Substring(endQuote + 1).Trim();
            }
        }
        else
        {
            // Unquoted - find first space separator
            var firstSpace = command.IndexOf(' ');
            if (firstSpace == -1)
            {
                fileName = command;
                arguments = string.Empty;
            }
            else
            {
                fileName = command.Substring(0, firstSpace);
                arguments = command.Substring(firstSpace + 1).Trim();
            }
        }

        return (fileName, arguments);
    }

    /// <summary>
    /// Builds ProcessStartInfo with security settings.
    /// </summary>
    private static ProcessStartInfo BuildProcessStartInfo(string fileName, string arguments, string originalCommand)
    {
        var isShellBuiltIn = IsShellBuiltInCommand(fileName);
        var isHighRisk = ContainsCommandName(HighRiskCommands, fileName);
        
        // For high-risk commands, validate arguments more strictly
        if (isHighRisk && !string.IsNullOrEmpty(arguments))
        {
            ValidateHighRiskArguments(fileName, arguments);
        }

        if (isShellBuiltIn)
        {
            // Defense-in-depth: strip shell metacharacters that could enable command injection
            // even after upstream validation. The originalCommand has already passed IsValidCommand
            // and ContainsDangerousPatterns, but we sanitize again at the shell boundary.
            var sanitizedCommand = CommandInjectionValidator.SanitizeShellCommand(originalCommand);

            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /c \"{sanitizedCommand}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                LoadUserProfile = false
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Additional security: don't load user profile for high-risk commands
        if (isHighRisk)
        {
            startInfo.LoadUserProfile = false;
        }

        return startInfo;
    }

    /// <summary>
    /// Validates arguments for high-risk commands.
    /// </summary>
    private static void ValidateHighRiskArguments(string fileName, string arguments)
    {
        // Check for dangerous patterns in arguments
        if (CommandInjectionValidator.ContainsDangerousPatterns(arguments))
        {
            throw ExceptionHelper.DangerousPatternInArgs(fileName);
        }

        // Additional validation for specific commands
        var executable = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        
        switch (executable)
        {
            case "del":
            case "rd":
                ValidateDeleteCommandArguments(arguments);
                break;
            case "reg":
                ValidateRegCommandArguments(arguments);
                break;
        }
    }

    /// <summary>
    /// Validates delete command arguments to prevent deletion of system files.
    /// </summary>
    private static void ValidateDeleteCommandArguments(string arguments)
    {
        // Block deletion of system directories
        var systemPaths = new[] 
        { 
            @"c:\windows", @"c:\program files", @"c:\programdata",
            @"c:\users\", @"c:\system volume information", @"c:\$recycle.bin"
        };

        var lowerArgs = arguments.ToLowerInvariant();
        
        foreach (var sysPath in systemPaths)
        {
            if (lowerArgs.Contains(sysPath))
            {
                throw ExceptionHelper.DeletionSystemPathsNotAllowed();
            }
        }

        // Block wildcards that could match system files
        if (arguments.Contains("*.*") && !arguments.Contains("?") && !arguments.Contains("\\temp"))
        {
            throw ExceptionHelper.WildcardDeletionRestricted();
        }
    }

    /// <summary>
    /// Validates registry command arguments.
    /// </summary>
    private static void ValidateRegCommandArguments(string arguments)
    {
        var lowerArgs = arguments.ToLowerInvariant();
        
        // Block deletion of critical registry keys
        var criticalKeys = new[]
        {
            @"hkey_local_machine\system",
            @"hkey_local_machine\software\microsoft\windows",
            @"hkey_current_user\software\microsoft\windows"
        };

        foreach (var key in criticalKeys)
        {
            if (lowerArgs.Contains(key) && lowerArgs.Contains("delete"))
            {
                throw ExceptionHelper.DeletionCriticalRegistryNotAllowed();
            }
        }
    }

    /// <summary>
    /// Validates if a command is safe to execute.
    /// </summary>
    public static bool IsValidCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Check for command injection patterns
        if (CommandInjectionValidator.ContainsDangerousPatterns(command))
            return false;

        // Parse and validate the executable
        var (fileName, _) = ParseCommandLine(command);
        
        if (!IsAllowedExecutable(fileName))
            return false;

        return true;
    }

    /// <summary>
    /// Checks if an executable is in the allowlist.
    /// </summary>
    private static bool IsAllowedExecutable(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return ContainsCommandName(AllowedCommands, fileName);
    }

    private static bool ContainsCommandName(HashSet<string> commandNames, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var baseName = Path.GetFileName(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        return commandNames.Contains(fileName)
               || commandNames.Contains(baseName)
               || commandNames.Contains(nameWithoutExtension);
    }

    private static bool IsShellBuiltInCommand(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return string.Equals(name, "del", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "rd", StringComparison.OrdinalIgnoreCase);
    }

    internal async Task<bool> IsHighPerformancePowerPlanActiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await Task.WhenAll(process.WaitForExitAsync(cancellationToken), outputTask, errorTask).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch (Exception ex)
                {
                    Log.Instance.TraceOnce(
                        "opt-power-scheme-kill",
                        "Failed to kill power-scheme probe process after timeout/cancel.",
                        ex);
                }
            }

            var output = await outputTask.ConfigureAwait(false);
            return process.ExitCode == 0
                   && output.Contains(HighPerformancePowerSchemeGuid, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to evaluate active power plan.", ex);
            // Let TryGetActionAppliedAsync convert probe failures to an unknown
            // state. Returning false here would present an inaccessible machine
            // as a confidently unchecked optimization.
            throw;
        }
    }

    /// <summary>
    /// Validates action key format to prevent injection.
    /// </summary>
    private static bool IsValidActionKey(string actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
            return false;

        // Only allow alphanumeric, dots, dashes, and underscores
        // Pattern: ^[a-zA-Z0-9._-]+$
        return Regex.IsMatch(actionKey, @"^[a-zA-Z0-9._-]+$");
    }

    /// <summary>
    /// Validates service name format.
    /// </summary>
    private static bool IsValidServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        // Service names should be alphanumeric with limited special chars
        return Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_-]+$");
    }

    internal async Task ExecuteStartMenuDisableAsync(CancellationToken cancellationToken)
    {
        foreach (var tweak in WindowsOptimizationDefinitions.StartMenuDisableTweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsOptimizationHelper.ApplyRegistryTweak(tweak);
        }

        NotifyExplorerSettingsChanged();
        await ExplorerRestartHelper.RestartAsync().ConfigureAwait(false);
    }

    internal bool AreStartMenuTweaksApplied()
    {
        return WindowsOptimizationHelper.AreRegistryTweaksApplied(WindowsOptimizationDefinitions.StartMenuDisableTweaks);
    }

    internal async Task RevertStartMenuDisableAsync(CancellationToken cancellationToken)
    {
        foreach (var tweak in WindowsOptimizationDefinitions.StartMenuDisableTweaks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ToolkitRegistry.DeleteValue(tweak.Hive, tweak.SubKey, tweak.ValueName, true);
        }

        NotifyExplorerSettingsChanged();
        await ExplorerRestartHelper.RestartAsync().ConfigureAwait(false);
    }

    internal Task RevertPowerPlanAsync(CancellationToken cancellationToken) =>
        ExecuteCommandsSequentiallyAsync(
            cancellationToken,
            "powercfg -setactive SCHEME_BALANCED",
            "powercfg -h on");

    private static unsafe void NotifyExplorerSettingsChanged()
    {
        try
        {
            const string policy = "Policy";
            fixed (void* ptr = policy)
            {
                PInvoke.SendNotifyMessage(HWND.HWND_BROADCAST, PInvoke.WM_SETTINGCHANGE, 0, new IntPtr(ptr));
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to notify Explorer of settings change.", ex);
        }
    }
}

/// <summary>
/// Validator for detecting command injection attempts.
/// </summary>
public static class CommandInjectionValidator
{
    // Dangerous patterns that could indicate command injection
    private static readonly string[] DangerousPatterns = new[]
    {
        "&&",      // Command chaining
        "||",      // Command chaining
        "|",       // Pipe (check individually for non-redirection cases)
        ";",       // Command separator
        "`",       // PowerShell execution
        "$(",      // Command substitution
        "..",      // Directory traversal
        "../",     // Directory traversal
        "..\\",    // Directory traversal
        "%00",     // Null byte injection
        "${",      // Shell variable expansion
        "<(",      // Process substitution
    };

    // Regex patterns for more complex detection
    private static readonly Regex[] DangerousRegexPatterns = new[]
    {
        // Environment variable expansion (cmd.exe style)
        new Regex(@"%[a-zA-Z0-9_]+%", RegexOptions.Compiled),
        // PowerShell encoding/execution patterns
        new Regex(@"-[eE][nN][cC]?\s+", RegexOptions.Compiled),
        // Base64 encoded commands
        new Regex(@"-[eE][nN][cC]?\s+[a-zA-Z0-9+/]{100,}={0,2}", RegexOptions.Compiled),
        // Command substitution in PowerShell
        new Regex(@"\$\([^)]+\)", RegexOptions.Compiled),
        // IEX/Invoke-Expression patterns
        new Regex(@"[iI][eE][xX]|[iI]nvoke-[eE]xpression", RegexOptions.Compiled),
    };

    private static readonly Regex AllowedRedirectionPattern = new(
        @"(?<!\S)(?:[12]?>nul|[12]?>&[12])(?=$|\s)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Checks if input contains dangerous patterns that could indicate command injection.
    /// </summary>
    public static bool ContainsDangerousPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Check simple patterns
        foreach (var pattern in DangerousPatterns)
        {
            if (input.Contains(pattern, StringComparison.Ordinal))
                return true;
        }

        // Check regex patterns
        foreach (var regex in DangerousRegexPatterns)
        {
            if (regex.IsMatch(input))
                return true;
        }

        if (ContainsUnsafeRedirection(input))
            return true;

        // Check for single ampersand (command separator, not redirection)
        if (ContainsUnescapedAmpersand(input))
            return true;

        return false;
    }

    private static bool ContainsUnsafeRedirection(string input)
    {
        var withoutAllowedRedirection = AllowedRedirectionPattern.Replace(input, string.Empty);
        return withoutAllowedRedirection.Contains('>', StringComparison.Ordinal) ||
               withoutAllowedRedirection.Contains('<', StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks for command separator ampersands (not redirection patterns like 2>&1).
    /// </summary>
    private static bool ContainsUnescapedAmpersand(string input)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '&')
            {
                // Skip escaped ampersands (^&)
                if (i > 0 && input[i - 1] == '^')
                    continue;

                // Check if this is part of a redirection pattern (>&1, >&2, 2>&1, 1>&2)
                bool isRedirection = false;

                // Check for >&N pattern
                if (i > 0 && input[i - 1] == '>')
                {
                    if (i + 1 < input.Length && (input[i + 1] == '1' || input[i + 1] == '2'))
                    {
                        isRedirection = true;
                    }
                }
                // Check for N>&M pattern
                else if (i > 0 && (input[i - 1] == '1' || input[i - 1] == '2') && i > 1 && input[i - 2] == '>')
                {
                    if (i + 1 < input.Length && (input[i + 1] == '1' || input[i + 1] == '2'))
                    {
                        isRedirection = true;
                    }
                }
                // Check for >& (implicit descriptor)
                else if (i > 0 && input[i - 1] == '>' && (i + 1 >= input.Length || char.IsWhiteSpace(input[i + 1]) || input[i + 1] == '1' || input[i + 1] == '2'))
                {
                    isRedirection = true;
                }

                if (!isRedirection)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sanitizes input by removing dangerous characters.
    /// </summary>
    public static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sanitized = input;
        
        foreach (var pattern in DangerousPatterns)
        {
            sanitized = sanitized.Replace(pattern, string.Empty, StringComparison.Ordinal);
        }

        return sanitized;
    }

    // Shell metacharacters that are dangerous when passed to cmd.exe /c
    private static readonly char[] ShellMetaChars = ['&', '|', ';', '>', '<', '^', '%', '`'];

    /// <summary>
    /// Sanitizes a command string for safe use with cmd.exe /c by stripping shell metacharacters
    /// and escaping embedded double quotes. This is a defense-in-depth layer applied at the
    /// shell boundary in addition to upstream validation.
    /// </summary>
    public static string SanitizeShellCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
            return command;

        // Remove shell metacharacters that could enable injection
        var chars = new char[command.Length];
        var idx = 0;
        foreach (var c in command)
        {
            if (Array.IndexOf(ShellMetaChars, c) >= 0)
                continue;
            chars[idx++] = c;
        }

        // Escape embedded double quotes to prevent breaking out of the /c "..." wrapper
        return new string(chars, 0, idx).Replace("\"", "\\\"");
    }
}
