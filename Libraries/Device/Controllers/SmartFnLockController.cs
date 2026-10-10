using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using NeoSmart.AsyncLock;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace UniversalDeviceToolkit.Lib.Controllers;

public class SmartFnLockController(FnLockFeature feature, ApplicationSettings settings) : IDisposable
{
    private readonly AsyncLock _lock = new();

    private bool _restoreFnLock;

    public void OnKeyboardEvent(nuint wParam, KBDLLHOOKSTRUCT kbStruct)
    {
        if (settings.Store.SmartFnLockFlags == 0)
            return;

        // Fast synchronous filter: only modifier-key transitions and the
        // non-modifier key that triggers FnLock restoration actually need
        // asynchronous work. Spawning Task.Run on every keystroke floods the
        // thread pool and serializes behind the AsyncLock, which makes the
        // whole system feel sluggish while typing.
        var vkKeyCode = (VIRTUAL_KEY)kbStruct.vkCode;
        var isModifierKey = vkKeyCode is VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL
            or VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT
            or VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU;

        if (!isModifierKey && !_restoreFnLock)
            return;

        Task.Run(async () =>
        {
            try
            {
                using (await _lock.LockAsync().ConfigureAwait(false))
                    await OnKeyboardEventAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to handle keyboard event.", ex);
            }
        });
    }

    private async Task OnKeyboardEventAsync()
    {
        if (IsModifierKeyPressed())
        {
            if (_restoreFnLock)
                return;

            var state = await feature.GetStateAsync().ConfigureAwait(false);
            if (state == FnLockState.Off)
                return;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Disabling Fn Lock temporarily...");

            await feature.SetStateAsync(FnLockState.Off).ConfigureAwait(false);
            _restoreFnLock = true;
        }
        else if (_restoreFnLock)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Re-enabling Fn Lock...");

            await feature.SetStateAsync(FnLockState.On).ConfigureAwait(false);
            _restoreFnLock = false;
        }
    }

    /// <summary>
    /// Query live modifier state via GetKeyState rather than tracking
    /// hook-event order, which can desync on KEYUP/SYSKEY transitions.
    /// (CsWin32 surface exposes GetKeyState; high bit = currently down.)
    /// </summary>
    private bool IsModifierKeyPressed()
    {
        static bool IsDown(VIRTUAL_KEY key) => (PInvoke.GetKeyState((int)key) & 0x8000) != 0;

        var ctrlDown = IsDown(VIRTUAL_KEY.VK_LCONTROL)
            || IsDown(VIRTUAL_KEY.VK_RCONTROL)
            || IsDown(VIRTUAL_KEY.VK_CONTROL);
        var shiftDown = IsDown(VIRTUAL_KEY.VK_LSHIFT)
            || IsDown(VIRTUAL_KEY.VK_RSHIFT)
            || IsDown(VIRTUAL_KEY.VK_SHIFT);
        var altDown = IsDown(VIRTUAL_KEY.VK_LMENU)
            || IsDown(VIRTUAL_KEY.VK_RMENU)
            || IsDown(VIRTUAL_KEY.VK_MENU);

        var flags = settings.Store.SmartFnLockFlags;
        var result = false;

        if (flags.HasFlag(ModifierKey.Ctrl))
            result |= ctrlDown;

        if (flags.HasFlag(ModifierKey.Shift))
            result |= shiftDown;

        if (flags.HasFlag(ModifierKey.Alt))
            result |= altDown;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Modifier key is depressed: {result} [ctrl={ctrlDown}, shift={shiftDown}, alt={altDown}, flags={flags}]");

        return result;
    }

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
            }
            _disposed = true;
        }
    }
}
