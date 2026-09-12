using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

/// <summary>
/// Represents a single step in an automation pipeline that can check support, execute, and be deep-copied.
/// </summary>
public interface IAutomationStep
{
    /// <summary>
    /// Determines whether this automation step is supported on the current system.
    /// </summary>
    /// <returns><c>true</c> if the step is supported; otherwise, <c>false</c>.</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// Executes the automation step within the specified context and environment.
    /// </summary>
    /// <param name="context">The automation context providing access to services and state.</param>
    /// <param name="environment">The automation environment containing device-specific settings.</param>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token);

    /// <summary>
    /// Creates a deep copy of this automation step.
    /// </summary>
    /// <returns>A new <see cref="IAutomationStep"/> instance with identical state.</returns>
    IAutomationStep DeepCopy();
}

/// <summary>
/// Represents a typed automation step that exposes a state value and all available states.
/// </summary>
/// <typeparam name="T">The value type representing the step's state.</typeparam>
public interface IAutomationStep<T> : IAutomationStep where T : struct
{
    /// <summary>
    /// Gets the current state of this automation step.
    /// </summary>
    T State { get; }

    /// <summary>
    /// Retrieves all possible states available for this automation step.
    /// </summary>
    /// <returns>An array of all available states.</returns>
    Task<T[]> GetAllStatesAsync();
}
