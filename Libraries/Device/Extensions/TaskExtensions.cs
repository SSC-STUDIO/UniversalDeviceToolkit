using System.Threading.Tasks;

using System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class TaskExtensions
{
    public static ValueTask AsValueTask(this Task task) => new(task);

    public static async Task<T?> OrNullIfException<T>(this Task<T> task) where T : struct
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Task completed with an exception; returning null.", ex);

            return null;
        }
    }

    public static void Forget(this Task task, string operationName)
    {
        _ = ObserveAsync(task, operationName);
    }

    private static async Task ObserveAsync(Task task, string operationName)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Background task failed: {operationName}.", ex);
        }
    }
}
