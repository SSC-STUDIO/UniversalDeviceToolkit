using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32.TaskScheduler;

namespace UniversalDeviceToolkit.Lib.System;

public static class Autorun
{
    private const string TASK_NAME = AppIdentity.CompactName + "_Autorun_6efcc882-924c-4cbc-8fec-f45c25696f98";
    private const string LEGACY_TASK_NAME = AppIdentity.LegacyCompactName + "_Autorun_6efcc882-924c-4cbc-8fec-f45c25696f98";
    private const string ShellPathVariable = "UDT_SHELL_PATH";
    private const string ShellArgsVariable = "UDT_SHELL_ARGS";

    public static AutorunState State
    {
        get
        {
            var task = TaskService.Instance.GetTask(TASK_NAME) ?? TaskService.Instance.GetTask(LEGACY_TASK_NAME);
            if (task is null)
                return AutorunState.Disabled;

            var delayed = task.Definition.Triggers.OfType<LogonTrigger>().FirstOrDefault()?.Delay > TimeSpan.Zero;
            return delayed ? AutorunState.EnabledDelayed : AutorunState.Enabled;
        }
    }

    public static void Validate()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Validating autorun...");

        var currentTask = TaskService.Instance.GetTask(TASK_NAME) ?? TaskService.Instance.GetTask(LEGACY_TASK_NAME);
        if (currentTask is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Autorun is not enabled.");
            return;
        }

        var mainModule = Process.GetCurrentProcess().MainModule;
        if (mainModule is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Main module is null.");
            return;
        }

        var fileVersion = mainModule.FileVersionInfo.FileVersion;
        if (fileVersion is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"File version is null.");
            return;
        }

        if (currentTask.Definition.Data == fileVersion &&
            currentTask.Definition.Principal.RunLevel == TaskRunLevel.LUA)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Autorun settings seems to be fine.");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Enabling autorun again...");

        var delayed = currentTask.Definition.Triggers.OfType<LogonTrigger>().FirstOrDefault()?.Delay > TimeSpan.Zero;

        Enable(delayed);
    }

    public static void Set(AutorunState state)
    {
        if (state == AutorunState.Disabled)
            Disable();
        else
            Enable(state == AutorunState.EnabledDelayed);
    }

    /// <summary>
    /// Electron sets <c>UDT_SHELL_PATH</c> to the UI executable so login starts
    /// the shell (which then spawns Host), not Host.exe by itself.
    /// </summary>
    private static string ResolveLaunchPath(string processPath)
    {
        var shell = Environment.GetEnvironmentVariable(ShellPathVariable);
        if (!string.IsNullOrWhiteSpace(shell) && File.Exists(shell))
            return shell;
        return processPath;
    }

    private static string ResolveLaunchArguments()
    {
        var args = Environment.GetEnvironmentVariable(ShellArgsVariable);
        return string.IsNullOrWhiteSpace(args) ? "--minimized" : args;
    }

    private static void Enable(bool delayed)
    {
        var mainModule = Process.GetCurrentProcess().MainModule ?? throw ExceptionHelper.MainModuleNull();
        var processPath = mainModule.FileName ?? throw ExceptionHelper.CurrentProcessFileNameNull();
        var filename = ResolveLaunchPath(processPath);
        var fileVersion = mainModule.FileVersionInfo.FileVersion ?? throw ExceptionHelper.CurrentProcessFileVersionNull();
        var currentUser = WindowsIdentity.GetCurrent().Name;

        var ts = TaskService.Instance;
        var td = ts.NewTask();
        td.Data = fileVersion;
        td.Principal.UserId = currentUser;
        td.Principal.RunLevel = TaskRunLevel.LUA;
        td.Triggers.Add(new LogonTrigger { UserId = currentUser, Delay = new TimeSpan(0, 0, delayed ? 30 : 0) });
        td.Actions.Add($"\"{filename}\"", ResolveLaunchArguments());
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        ts.RootFolder.RegisterTaskDefinition(TASK_NAME, td);
        DeleteTask(LEGACY_TASK_NAME, "Legacy autorun disabled");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Autorun enabled");
    }

    private static void Disable()
    {
        var deleted = DeleteTask(TASK_NAME, "Autorun disabled");
        var legacyDeleted = DeleteTask(LEGACY_TASK_NAME, "Legacy autorun disabled");

        if (!deleted && !legacyDeleted && Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Autorun was not enabled");
    }

    private static bool DeleteTask(string taskName, string successMessage)
    {
        try
        {
            TaskService.Instance.RootFolder.DeleteTask(taskName);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(successMessage);

            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.WarningOnce(
                $"autorun-delete-{taskName}",
                $"Failed to delete autorun scheduled task '{taskName}'.",
                ex);
            return false;
        }
    }
}
