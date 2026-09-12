using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Utils;

public class MaximumRetriesReachedException : Exception
{
    public MaximumRetriesReachedException() { }
    public MaximumRetriesReachedException(string message) : base(message) { }
    public MaximumRetriesReachedException(string message, Exception innerException) : base(message, innerException) { }
}

public static class RetryHelper
{
    public static async Task RetryAsync(Func<Task> action,
        int? maximumRetries = null,
        TimeSpan? timeout = null,
        Func<Exception, bool>? matchingException = null,
        IDelayProvider? delayProvider = null,
        [CallerMemberName] string? tag = null)
    {
        maximumRetries ??= 3;
        timeout ??= TimeSpan.FromMilliseconds(500);
        matchingException ??= (ex) => ex is not OperationCanceledException;

        var retries = 0;
        while (true)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                if (ex is MaximumRetriesReachedException)
                    throw;

                if (ex is OperationCanceledException)
                    throw;

                if (!matchingException(ex))
                    throw;

                retries++;

                if (retries >= maximumRetries)
                    throw new MaximumRetriesReachedException($"Maximum retries ({maximumRetries}) reached. [tag={tag}]", ex);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Retrying {retries}/{maximumRetries}... [tag={tag}]");

                var delay = TimeSpan.FromMilliseconds(timeout.Value.TotalMilliseconds * (1 << (retries - 1)));
                if (delayProvider is not null)
                    await delayProvider.Delay(delay, CancellationToken.None).ConfigureAwait(false);
                else
                    await Task.Delay(delay).ConfigureAwait(false);
            }
        }
    }
}
