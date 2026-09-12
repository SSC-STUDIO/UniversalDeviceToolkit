using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace UniversalDeviceToolkit.Lib.GameDetection;

internal unsafe class EffectiveGameModeDetector
{
    private readonly EFFECTIVE_POWER_MODE_CALLBACK _callbackPointer;

    private IntPtr _handle;
    private bool? _lastState;

    public event EventHandler<bool>? Changed;

    public EffectiveGameModeDetector()
    {
        _callbackPointer = Callback;
    }

    public Task StartAsync()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("EFFECTIVE_POWER_MODE_V2 not supported on this Windows version.");
            return Task.CompletedTask;
        }

        var result = PInvoke.PowerRegisterForEffectivePowerModeNotifications(PInvoke.EFFECTIVE_POWER_MODE_V2, _callbackPointer, null, out var handle);
        if (result == 0)
            _handle = new IntPtr(handle);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_handle != IntPtr.Zero)
        {
            PInvoke.PowerUnregisterFromEffectivePowerModeNotifications(_handle.ToPointer());
            _handle = IntPtr.Zero;
        }
        return Task.CompletedTask;
    }

    private void Callback(EFFECTIVE_POWER_MODE mode, void* context)
    {
        var state = mode == EFFECTIVE_POWER_MODE.EffectivePowerModeGameMode;

        _lastState ??= state;

        if (_lastState == state)
            return;

        _lastState = state;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Effective power mode changed to {mode}.");

        Changed?.Invoke(this, state);
    }
}
