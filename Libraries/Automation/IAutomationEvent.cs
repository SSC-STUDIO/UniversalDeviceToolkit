using System;

namespace UniversalDeviceToolkit.Lib.Automation;

/// <summary>
/// Marker interface for all automation events that can be dispatched through the automation pipeline.
/// </summary>
public interface IAutomationEvent;

/// <summary>
/// Represents an automation event triggered by HDR state changes on the display.
/// </summary>
public readonly struct HDRAutomationEvent(bool? isHDROn) : IAutomationEvent
{
    /// <summary>
    /// Gets whether HDR is currently enabled, or <c>null</c> if the state is unknown.
    /// </summary>
    public bool? IsHDROn { get; } = isHDROn;
}

/// <summary>
/// Represents an automation event triggered by a native Windows system message.
/// </summary>
public readonly struct NativeWindowsMessageEvent(NativeWindowsMessage message, object? data) : IAutomationEvent
{
    /// <summary>
    /// Gets the type of native Windows message that triggered this event.
    /// </summary>
    public NativeWindowsMessage Message { get; } = message;
    /// <summary>
    /// Gets optional data associated with the message.
    /// </summary>
    public object? Data { get; } = data;
}

/// <summary>
/// Represents an automation event triggered when the application starts up.
/// </summary>
public struct StartupAutomationEvent : IAutomationEvent;

/// <summary>
/// Represents an automation event triggered by power state changes (e.g., AC adapter connection or disconnection).
/// </summary>
public readonly struct PowerStateAutomationEvent(PowerStateEvent powerStateEvent, bool powerAdapterStateChanged)
    : IAutomationEvent
{
    /// <summary>
    /// Gets the power state event type.
    /// </summary>
    public PowerStateEvent PowerStateEvent { get; } = powerStateEvent;
    /// <summary>
    /// Gets a value indicating whether the power adapter state changed.
    /// </summary>
    public bool PowerAdapterStateChanged { get; } = powerAdapterStateChanged;
}

/// <summary>
/// Represents an automation event triggered by a power mode change (e.g., Quiet, Balance, Performance).
/// </summary>
public readonly struct PowerModeAutomationEvent(PowerModeState powerModeState) : IAutomationEvent
{
    /// <summary>
    /// Gets the new power mode state.
    /// </summary>
    public PowerModeState PowerModeState { get; } = powerModeState;
}

/// <summary>
/// Represents an automation event triggered when a custom mode (God Mode) preset is activated.
/// </summary>
public readonly struct CustomModePresetAutomationEvent(Guid id) : IAutomationEvent
{
    /// <summary>
    /// Gets the unique identifier of the activated custom mode preset.
    /// </summary>
    public Guid Id { get; } = id;
}

/// <summary>
/// Represents an automation event triggered by game launch or exit.
/// </summary>
public readonly struct GameAutomationEvent(bool running) : IAutomationEvent
{
    /// <summary>
    /// Gets a value indicating whether a game is currently running.
    /// </summary>
    public bool Running { get; } = running;
}

/// <summary>
/// Represents an automation event triggered by a process start or stop.
/// </summary>
public readonly struct ProcessAutomationEvent(ProcessEventInfoType type, ProcessInfo processInfo) : IAutomationEvent
{
    /// <summary>
    /// Gets the type of process event (started or stopped).
    /// </summary>
    public ProcessEventInfoType Type { get; } = type;

    /// <summary>
    /// Gets the information about the process that triggered this event.
    /// </summary>
    public ProcessInfo ProcessInfo { get; } = processInfo;
}

/// <summary>
/// Represents an automation event triggered when the user session is locked or unlocked.
/// </summary>
public readonly struct SessionLockUnlockAutomationEvent(bool locked) : IAutomationEvent
{
    /// <summary>
    /// Gets a value indicating whether the session was locked (<c>true</c>) or unlocked (<c>false</c>).
    /// </summary>
    public bool Locked { get; } = locked;
}

/// <summary>
/// Represents an automation event triggered by a time-based condition.
/// </summary>
public readonly struct TimeAutomationEvent(Time time, DayOfWeek day) : IAutomationEvent
{
    /// <summary>
    /// Gets the time that triggered this event.
    /// </summary>
    public Time Time { get; } = time;
    /// <summary>
    /// Gets the day of the week when this event was triggered.
    /// </summary>
    public DayOfWeek Day { get; } = day;
}

/// <summary>
/// Represents an automation event triggered after a period of user inactivity.
/// </summary>
public readonly struct UserInactivityAutomationEvent(TimeSpan inactivityTimeSpan)
    : IAutomationEvent
{
    /// <summary>
    /// Gets the duration of user inactivity that triggered this event.
    /// </summary>
    public TimeSpan InactivityTimeSpan { get; } = inactivityTimeSpan;
}

/// <summary>
/// Represents an automation event triggered by WiFi connection state changes.
/// </summary>
public readonly struct WiFiAutomationEvent(bool isConnected, string? ssid) : IAutomationEvent
{
    /// <summary>
    /// Gets a value indicating whether the WiFi is currently connected.
    /// </summary>
    public bool IsConnected { get; } = isConnected;
    /// <summary>
    /// Gets the SSID of the connected WiFi network, or <c>null</c> if disconnected.
    /// </summary>
    public string? Ssid { get; } = ssid;
}
