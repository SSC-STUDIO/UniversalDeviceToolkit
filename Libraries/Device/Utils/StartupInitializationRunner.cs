using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// A single startup initialization step. <see cref="Name"/> must be unique
/// within a runner instance and is used both for timeout lookup on the
/// supplied <see cref="StartupHealthGuard"/> and for logging.
/// </summary>
public sealed class StartupStep
{
    public StartupStep(string name, TimeSpan timeout, Action action, bool isCritical = true)
        : this(name, timeout, isCritical)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        IsAsync = false;
    }

    public StartupStep(string name, TimeSpan timeout, Func<Task> action, bool isCritical = true)
        : this(name, timeout, isCritical)
    {
        AsyncAction = action ?? throw new ArgumentNullException(nameof(action));
        IsAsync = true;
    }

    private StartupStep(string name, TimeSpan timeout, bool isCritical)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Step name must not be null or whitespace.", nameof(name));
        Name = name;
        Timeout = timeout;
        IsCritical = isCritical;
    }

    public string Name { get; }
    public TimeSpan Timeout { get; }
    public bool IsCritical { get; }
    public bool IsAsync { get; }
    public Action? Action { get; }
    public Func<Task>? AsyncAction { get; }
}

/// <summary>
/// Aggregated outcome of a <see cref="StartupInitializationRunner.RunAsync"/>
/// pass. <see cref="Success"/> is true only when every critical step completed
/// without error; non-critical failures are listed in
/// <see cref="FailedSteps"/> but do not flip the success bit. When the host
/// asked for safe-start, <see cref="EnteredSafeMode"/> is true and
/// <see cref="SkippedSteps"/> carries the names of the steps that were
/// intentionally bypassed.
/// </summary>
public sealed record StartupInitializationResult(
    bool Success,
    IReadOnlyList<string> FailedSteps,
    bool EnteredSafeMode,
    IReadOnlyList<string> SkippedSteps)
{
    public static readonly StartupInitializationResult Empty =
        new(true, Array.Empty<string>(), false, Array.Empty<string>());
}

/// <summary>
/// Runs startup initialization steps in registration order. Critical step
/// failures short-circuit the run; non-critical failures are recorded but the
/// runner keeps going. When constructed with <see cref="SafeStart"/> = true,
/// non-critical steps are skipped entirely instead of running.
/// </summary>
public sealed class StartupInitializationRunner
{
    private readonly StartupHealthGuard _guard;
    private readonly bool _safeStart;
    private readonly List<StartupStep> _steps = new();
    private readonly List<string> _skippedStepNames = new();

    public StartupInitializationRunner(StartupHealthGuard guard)
        : this(guard, safeStart: false)
    {
    }

    public StartupInitializationRunner(StartupHealthGuard guard, bool safeStart)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _safeStart = safeStart;
    }

    /// <summary>
    /// Adds a step to the runner and registers its timeout on the supplied
    /// <see cref="StartupHealthGuard"/> so a direct
    /// <c>TryRunStep</c> against the same name observes the same budget.
    /// </summary>
    public void RegisterStep(StartupStep step)
    {
        if (step is null) throw new ArgumentNullException(nameof(step));

        if (_safeStart && !step.IsCritical)
        {
            _skippedStepNames.Add(step.Name);

            try
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"SafeStart active: skipping step '{step.Name}' (non-critical).");
            }
            catch { /* Trace logging must not block registration */ }

            return;
        }

        _guard.RegisterStep(step.Name, step.Timeout);
        _steps.Add(step);
    }

    /// <summary>
    /// Convenience overload for callers that already have the four-argument
    /// constructor of <see cref="StartupStep"/> available.
    /// </summary>
    public void RegisterStep(string name, TimeSpan timeout, Action action, bool isCritical = true) =>
        RegisterStep(new StartupStep(name, timeout, action, isCritical));

    /// <summary>
    /// Convenience overload for callers that want to register an async step.
    /// </summary>
    public void RegisterStep(string name, TimeSpan timeout, Func<Task> action, bool isCritical = true) =>
        RegisterStep(new StartupStep(name, timeout, action, isCritical));

    /// <summary>
    /// Executes the registered steps in order. A critical step failure stops
    /// the run with <see cref="StartupInitializationResult.Success"/> = false;
    /// a non-critical failure is collected and the runner continues with the
    /// next step. When <see cref="SafeStart"/> is true at construction time,
    /// non-critical steps are skipped entirely and their names land in
    /// <see cref="StartupInitializationResult.SkippedSteps"/>.
    /// </summary>
    public async Task<StartupInitializationResult> RunAsync(CancellationToken ct = default)
    {
        var skipped = new List<string>(_skippedStepNames);

        if (_steps.Count == 0)
            return new StartupInitializationResult(true, Array.Empty<string>(), _safeStart, skipped);

        var failed = new List<string>();
        var criticalFailure = false;

        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();

            bool ok;
            Exception? error;
            try
            {
                if (step.IsAsync && step.AsyncAction is not null)
                {
                    var (asyncOk, asyncError) = await _guard
                        .TryRunStepAsync(step.Name, step.AsyncAction)
                        .ConfigureAwait(false);
                    ok = asyncOk;
                    error = asyncError;
                }
                else if (step.Action is not null)
                {
                    ok = _guard.TryRunStep(step.Name, step.Action, out error);
                }
                else
                {
                    ok = false;
                    error = new InvalidOperationException(
                        $"Step '{step.Name}' has no action; mis-registered.");
                }
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (StackOverflowException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ok = false;
                error = ex;
            }

            if (ok)
                continue;

            failed.Add(step.Name);

            if (step.IsCritical)
            {
                criticalFailure = true;
                return new StartupInitializationResult(
                    false, failed, _safeStart, skipped);
            }
        }

        return new StartupInitializationResult(
            !criticalFailure, failed, _safeStart, skipped);
    }
}
