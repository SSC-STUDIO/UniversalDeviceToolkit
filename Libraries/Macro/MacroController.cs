using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Macro;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Macro.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace UniversalDeviceToolkit.Lib.Macro;

public class MacroController : IMacroController, IDisposable
{
    public class RecorderReceivedEventArgs : EventArgs
    {
        public MacroEvent MacroEvent { get; init; }
    }

    public class RecorderStoppedEventArgs : EventArgs
    {
        public bool Interrupted { get; init; }
    }

    private static readonly uint[] AllowedKeys = [0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69];
    public static readonly int[] AllowedRepeatCounts = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private readonly MacroRecorder _recorder = new();
    private readonly MacroPlayer _player = new();

    private readonly HOOKPROC _kbProc;
    private readonly MacroSettings _settings;
    private readonly object _hookSync = new();

    private HHOOK _kbHook;
    private MacroHookPump? _pump;
    private bool _disposed;

    public event EventHandler<RecorderReceivedEventArgs>? RecorderReceived;
    public event EventHandler<RecorderStoppedEventArgs>? RecorderStopped;

    public bool IsEnabled => _settings.Store.IsEnabled;

    /// <summary>
    /// Gets whether the playback WH_KEYBOARD_LL hook is installed on a live
    /// message-pump thread. Distinct from <see cref="IsEnabled"/>: settings can
    /// say enabled while the hook is not yet (or no longer) running.
    /// </summary>
    public bool IsHookActive
    {
        get
        {
            lock (_hookSync)
                return _pump is { IsActive: true };
        }
    }

    /// <summary>
    /// Gets whether the recorder currently owns an input hook.
    /// </summary>
    public bool IsRecording => _recorder.IsRecording;

    public MacroController(MacroSettings settings, IMainThreadDispatcher mainThreadDispatcher)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mainThreadDispatcher);

        _settings = settings;
        _kbProc = LowLevelKeyboardProc;

        _recorder.Received += Recorder_Received;
        _recorder.Stopped += Recorder_Stopped;

        // Restore a previously persisted enable flag: the headless host never
        // calls Start() on its own, so the pump must come up with the controller.
        if (IsEnabled && !Start())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Macro playback hook was not installed at startup.");
        }
    }

    private void Recorder_Received(object? sender, MacroRecorder.ReceivedEventArgs e) => RecorderReceived?.Invoke(this, new() { MacroEvent = e.MacroEvent });

    private void Recorder_Stopped(object? sender, MacroRecorder.StoppedEventArgs e) => RecorderStopped?.Invoke(this, new() { Interrupted = e.Interrupted });

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!Start())
                throw new MacroHookInstallException(
                    "Playback hook could not be installed (SetWindowsHookEx failed); try again after the host has an interactive session.");

            _settings.Store.IsEnabled = true;
            _settings.SynchronizeStore();
            return;
        }

        _settings.Store.IsEnabled = false;
        _settings.SynchronizeStore();
        Stop();
    }

    public Dictionary<MacroIdentifier, MacroSequence> GetSequences() => _settings.Store.Sequences;

    /// <summary>
    /// Starts playback for a stored keyboard sequence. The method is intentionally
    /// non-blocking, matching the global hook playback behavior.
    /// </summary>
    public bool TryPlaySequence(ulong key)
    {
        var identifier = new MacroIdentifier(MacroSource.Keyboard, key);
        if (!_settings.Store.Sequences.TryGetValue(identifier, out var sequence) || sequence.Events is not { Length: > 0 })
            return false;

        Play(sequence);
        return true;
    }

    public void SetSequences(Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        CleanUp(ref sequences);

        _settings.Store.Sequences = sequences;
        _settings.SynchronizeStore();
    }

    /// <summary>
    /// Installs the playback hook on a dedicated message-pump thread and waits
    /// for the install result. Returns false when SetWindowsHookEx fails or the
    /// pump does not come up in time.
    /// </summary>
    public bool Start()
    {
        MacroHookPump pump;
        lock (_hookSync)
        {
            if (_pump is { IsActive: true })
                return true;

            _pump?.Dispose();
            pump = new MacroHookPump("MacroPlaybackHook", InstallPlaybackHook, UninstallPlaybackHook);
            _pump = pump;
        }

        if (!pump.Start(TimeSpan.FromSeconds(5)))
        {
            lock (_hookSync)
            {
                if (ReferenceEquals(_pump, pump))
                {
                    pump.Dispose();
                    _pump = null;
                }
            }

            return false;
        }

        lock (_hookSync)
        {
            if (!ReferenceEquals(_pump, pump) || !pump.IsActive)
                return false;
        }

        return true;
    }

    public void Stop()
    {
        MacroHookPump? pump;
        lock (_hookSync)
        {
            pump = _pump;
            _pump = null;
        }

        // Unhook on the installing pump thread first. A throw from recorder or
        // player teardown must never leave the hook installed.
        try
        {
            pump?.Stop();
            pump?.Dispose();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("MacroController.Stop() pump teardown failed.", ex);
        }

        try
        {
            _recorder.StopRecording();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("MacroController.Stop() recorder teardown failed.", ex);
        }

        try
        {
            _player.Stop();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("MacroController.Stop() player teardown failed.", ex);
        }
    }

    /// <summary>Test seam: replace SetWindowsHookEx so install failure can be forced.</summary>
    internal static Func<bool>? HookInstallOverride { get; set; }

    private bool InstallPlaybackHook()
    {
        if (HookInstallOverride is not null)
            return HookInstallOverride();

        if (_kbHook != default)
            return true;

        _kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _kbProc, HINSTANCE.Null, 0);
        return _kbHook != default;
    }

    private void UninstallPlaybackHook()
    {
        var hook = _kbHook;
        _kbHook = default;
        if (hook == default)
            return;

        try
        {
            if (!PInvoke.UnhookWindowsHookEx(hook) && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("MacroController.UnhookWindowsHookEx returned false (hook may already be invalid).");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("MacroController.Stop() unhook failed.", ex);
        }
    }

    public void StartRecording(MacroRecorderSettings settings = MacroRecorderSettings.Keyboard) => _recorder.StartRecording(settings);

    public void StopRecording() => _recorder.StopRecording();

    private unsafe LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode != PInvoke.HC_ACTION)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        if (!IsEnabled)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        ref var kbStruct = ref Unsafe.AsRef<KBDLLHOOKSTRUCT>((void*)lParam.Value);

        _player.InterruptIfNeeded(kbStruct);

        var shouldRun = !_recorder.IsRecording;
        shouldRun &= kbStruct.flags == 0;
        shouldRun &= _settings.Store.Sequences.GetValueOrNull(new(MacroSource.Keyboard, kbStruct.vkCode))?.Events?.Length > 0;
        shouldRun &= AllowedKeys.Contains(kbStruct.vkCode);

        if (!shouldRun)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        var sequence = _settings.Store.Sequences[new(MacroSource.Keyboard, kbStruct.vkCode)];
        Play(sequence);

        return new LRESULT(96);
    }

    private void Play(MacroSequence sequence) => _ = PlayAsync(sequence);

    private async Task PlayAsync(MacroSequence sequence)
    {
        try
        {
            await _player.StartPlayingAsync(sequence).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error playing macro sequence: {ex.Message}", ex);
        }
    }

    private static void CleanUp(ref Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        sequences = ClearDownsWithoutUps(sequences);
        sequences = ClearEmptySequences(sequences);
    }

    private static Dictionary<MacroIdentifier, MacroSequence> ClearEmptySequences(Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        return sequences.Where(kv => kv.Value.Events?.Length > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static Dictionary<MacroIdentifier, MacroSequence> ClearDownsWithoutUps(Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        var result = new Dictionary<MacroIdentifier, MacroSequence>();
        foreach (var kv in sequences)
            result[kv.Key] = ClearDownsWithoutUps(kv.Value);
        return result;
    }

    private static MacroSequence ClearDownsWithoutUps(MacroSequence sequence)
    {
        var macroEvents = new List<MacroEvent>(sequence.Events ?? []);
        for (var i = macroEvents.Count - 1; i >= 0; i--)
        {
            var macroEvent = macroEvents[i];

            switch (macroEvent.Direction)
            {
                case MacroDirection.Down:
                    {
                        var remove = true;

                        for (var j = i; j < macroEvents.Count; j++)
                        {
                            if (macroEvents[j].Direction != MacroDirection.Up || macroEvents[j].Key != macroEvent.Key || macroEvents[j].Source != macroEvent.Source)
                                continue;

                            remove = false;
                            break;
                        }

                        if (remove)
                            macroEvents.RemoveAt(i);
                        break;
                    }
                case MacroDirection.Up:
                case MacroDirection.Unknown:
                default:
                    continue;
            }
        }

        return sequence with { Events = [.. macroEvents] };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Stop();
            _recorder.Received -= Recorder_Received;
            _recorder.Stopped -= Recorder_Stopped;
        }

        _disposed = true;
    }
}
