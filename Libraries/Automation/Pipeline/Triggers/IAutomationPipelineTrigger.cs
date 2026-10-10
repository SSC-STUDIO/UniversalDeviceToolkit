using System;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// Represents a trigger that can match automation events or system states and update the automation environment.
/// </summary>
public interface IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the human-readable display name of this trigger.
    /// </summary>
    [JsonIgnore]
    string DisplayName { get; }

    /// <summary>
    /// Determines whether this trigger matches the specified automation event.
    /// </summary>
    /// <param name="automationEvent">The automation event to evaluate.</param>
    /// <returns><c>true</c> if the trigger matches the event; otherwise, <c>false</c>.</returns>
    Task<bool> IsMatchingEvent(IAutomationEvent automationEvent);

    /// <summary>
    /// Determines whether the current system state matches this trigger's conditions.
    /// </summary>
    /// <returns><c>true</c> if the current state matches; otherwise, <c>false</c>.</returns>
    Task<bool> IsMatchingState();

    /// <summary>
    /// Updates the automation environment with trigger-specific context.
    /// </summary>
    /// <param name="environment">The environment to update.</param>
    void UpdateEnvironment(AutomationEnvironment environment);

    /// <summary>
    /// Creates a deep copy of this trigger.
    /// </summary>
    /// <returns>A new <see cref="IAutomationPipelineTrigger"/> instance with identical configuration.</returns>
    IAutomationPipelineTrigger DeepCopy();
}

/// <summary>
/// Marker interface for triggers that disallow duplicate instances within the same pipeline.
/// </summary>
public interface IDisallowDuplicatesAutomationPipelineTrigger : IAutomationPipelineTrigger;


/// <summary>
/// Represents a composite trigger that groups multiple child triggers together.
/// </summary>
public interface ICompositeAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the child triggers that compose this composite trigger.
    /// </summary>
    public IAutomationPipelineTrigger[] Triggers { get; }
}

/// <summary>
/// Marker interface for triggers that respond to HDR state changes.
/// </summary>
public interface IHDRPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Represents a trigger that responds to native Windows system messages.
/// </summary>
public interface INativeWindowsMessagePipelineTrigger : IAutomationPipelineTrigger;

/// <summary>
/// Represents a trigger scoped to specific hardware devices identified by instance IDs.
/// </summary>
public interface IDeviceAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger
{
    /// <summary>
    /// Gets the hardware instance IDs this trigger monitors.
    /// </summary>
    string[] InstanceIds { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified instance IDs.
    /// </summary>
    /// <param name="instanceIds">The hardware instance IDs for the copy.</param>
    /// <returns>A new <see cref="IDeviceAutomationPipelineTrigger"/> with the given instance IDs.</returns>
    IDeviceAutomationPipelineTrigger DeepCopy(string[] instanceIds);
}

/// <summary>
/// Marker interface for triggers that fire when the application starts up.
/// </summary>
public interface IOnStartupAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Marker interface for triggers that fire when the system resumes from sleep or hibernation.
/// </summary>
public interface IOnResumeAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Marker interface for triggers that respond to power state changes (e.g., AC adapter events).
/// </summary>
public interface IPowerStateAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Represents a trigger that responds to power mode changes (e.g., Quiet, Balance, Performance).
/// </summary>
public interface IPowerModeAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the power mode state this trigger is configured for.
    /// </summary>
    PowerModeState PowerModeState { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified power mode state.
    /// </summary>
    /// <param name="powerModeState">The power mode state for the copy.</param>
    /// <returns>A new <see cref="IPowerModeAutomationPipelineTrigger"/> with the given state.</returns>
    IPowerModeAutomationPipelineTrigger DeepCopy(PowerModeState powerModeState);
}

/// <summary>
/// Represents a trigger that responds to custom mode (God Mode) preset changes.
/// </summary>
public interface IGodModePresetChangedAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the unique identifier of the custom mode preset.
    /// </summary>
    Guid PresetId { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified preset identifier.
    /// </summary>
    /// <param name="powerModeState">The preset identifier for the copy.</param>
    /// <returns>A new <see cref="IGodModePresetChangedAutomationPipelineTrigger"/> with the given preset ID.</returns>
    IGodModePresetChangedAutomationPipelineTrigger DeepCopy(Guid powerModeState);
}

/// <summary>
/// Marker interface for triggers that respond to game launch or exit events.
/// </summary>
public interface IGameAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Represents a trigger that responds to process start or stop events.
/// </summary>
public interface IProcessesAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the process definitions this trigger monitors.
    /// </summary>
    ProcessInfo[] Processes { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified processes.
    /// </summary>
    /// <param name="processes">The process definitions for the copy.</param>
    /// <returns>A new <see cref="IProcessesAutomationPipelineTrigger"/> with the given processes.</returns>
    IProcessesAutomationPipelineTrigger DeepCopy(ProcessInfo[] processes);
}

/// <summary>
/// Marker interface for triggers that fire when the user session is locked.
/// </summary>
public interface ISessionLockPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Marker interface for triggers that fire when the user session is unlocked.
/// </summary>
public interface ISessionUnlockPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Represents a trigger that responds to time-based conditions (sunrise, sunset, specific times, or days of week).
/// </summary>
public interface ITimeAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets a value indicating whether this trigger matches at sunrise.
    /// </summary>
    bool IsSunrise { get; }
    /// <summary>
    /// Gets a value indicating whether this trigger matches at sunset.
    /// </summary>
    bool IsSunset { get; }
    /// <summary>
    /// Gets the specific time this trigger matches, or <c>null</c> if not time-based.
    /// </summary>
    Time? Time { get; }
    /// <summary>
    /// Gets the days of the week this trigger is active on.
    /// </summary>
    DayOfWeek[] Days { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified time-based parameters.
    /// </summary>
    ITimeAutomationPipelineTrigger DeepCopy(bool isSunrise, bool isSunset, Time? time, DayOfWeek[] day);
}

/// <summary>
/// Represents a trigger that fires after a specified period of user inactivity.
/// </summary>
public interface IUserInactivityPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the inactivity duration required to activate this trigger.
    /// </summary>
    TimeSpan InactivityTimeSpan { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified inactivity time span.
    /// </summary>
    /// <param name="timeSpan">The inactivity duration for the copy.</param>
    /// <returns>A new <see cref="IUserInactivityPipelineTrigger"/> with the given time span.</returns>
    IUserInactivityPipelineTrigger DeepCopy(TimeSpan timeSpan);
}

/// <summary>
/// Represents a trigger that fires when a WiFi connection is established with matching SSIDs.
/// </summary>
public interface IWiFiConnectedPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the SSID patterns to match against connected WiFi networks.
    /// </summary>
    string[] Ssids { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified SSIDs.
    /// </summary>
    /// <param name="ssids">The SSID patterns for the copy.</param>
    /// <returns>A new <see cref="IWiFiConnectedPipelineTrigger"/> with the given SSIDs.</returns>
    IWiFiConnectedPipelineTrigger DeepCopy(string[] ssids);
}

/// <summary>
/// Marker interface for triggers that fire when WiFi is disconnected.
/// </summary>
public interface IWiFiDisconnectedPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// Represents a trigger that fires periodically at a specified interval.
/// </summary>
public interface IPeriodicAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>
    /// Gets the time interval between periodic firings.
    /// </summary>
    public TimeSpan Period { get; }

    /// <summary>
    /// Creates a deep copy of this trigger with the specified period.
    /// </summary>
    /// <param name="period">The interval for the copy.</param>
    /// <returns>A new <see cref="IPeriodicAutomationPipelineTrigger"/> with the given period.</returns>
    IPeriodicAutomationPipelineTrigger DeepCopy(TimeSpan period);
}
