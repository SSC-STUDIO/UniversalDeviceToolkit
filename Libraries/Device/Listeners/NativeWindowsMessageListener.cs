using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Listeners;

public class NativeWindowsMessageListener : IListener<NativeWindowsMessageListener.ChangedEventArgs>
{
    public class ChangedEventArgs(NativeWindowsMessage message, object? data = null) : EventArgs
    {
        public NativeWindowsMessage Message { get; } = message;
        public object? Data { get; } = data;
    }

    private readonly IMainThreadDispatcher _mainThreadDispatcher;
    private readonly DGPUNotify _dgpuNotify;
    private readonly SmartFnLockController _smartFnLockController;
    private readonly PowerModeFeature _powerModeFeature;
    private readonly IDelayProvider _delayProvider;

    private readonly HOOKPROC _kbProc;

    private readonly TaskCompletionSource _isMonitorOnTaskCompletionSource = new();
    private readonly TaskCompletionSource _isLidOpenTaskCompletionSource = new();

    // Touched only on the pump thread while the window is alive.
    private HDEVNOTIFY _deviceNotificationHandle;
    private HPOWERNOTIFY _consoleDisplayStateNotificationHandle;
    private HPOWERNOTIFY _lidSwitchStateChangeNotificationHandle;
    private HPOWERNOTIFY _powerSavingStateChangeNotificationHandle;
    private HHOOK _kbHook;

    private PumpedMessageWindow? _window;

    public bool IsMonitorOn { get; private set; }
    public bool IsLidOpen { get; private set; }

    public event EventHandler<ChangedEventArgs>? Changed;

    public NativeWindowsMessageListener(IMainThreadDispatcher mainThreadDispatcher, DGPUNotify dgpuNotify, SmartFnLockController smartFnLockController, PowerModeFeature powerModeFeature, IDelayProvider delayProvider)
    {
        _mainThreadDispatcher = mainThreadDispatcher;
        _dgpuNotify = dgpuNotify;
        _smartFnLockController = smartFnLockController;
        _powerModeFeature = powerModeFeature;
        _delayProvider = delayProvider;

        _kbProc = LowLevelKeyboardProc;
    }

    public async Task TurnOffMonitorAsync()
    {
        await _delayProvider.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
        await _mainThreadDispatcher.DispatchAsync(() =>
        {
            var hwnd = _window?.Hwnd ?? default;
            if (hwnd.IsNull)
                return Task.CompletedTask;

            PInvoke.SendMessage(hwnd, PInvoke.WM_SYSCOMMAND, new WPARAM(PInvoke.SC_MONITORPOWER), new LPARAM(2));
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    public Task StartAsync() => _mainThreadDispatcher.DispatchAsync(() =>
    {
        // The message-only window (and every hook / notification registered
        // against it) lives on the pump thread inside PumpedMessageWindow; the
        // headless dispatcher never pumps messages, so the window must own its
        // GetMessage loop.
        _window = new PumpedMessageWindow(
            "UniversalDeviceToolkit_MessageWindow",
            WndProc,
            onStarted: hwnd =>
            {
                _kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _kbProc, HINSTANCE.Null, 0);

                _deviceNotificationHandle = RegisterDeviceNotification(hwnd);
                _consoleDisplayStateNotificationHandle = RegisterPowerNotification(hwnd, PInvoke.GUID_CONSOLE_DISPLAY_STATE);
                _lidSwitchStateChangeNotificationHandle = RegisterPowerNotification(hwnd, PInvoke.GUID_LIDSWITCH_STATE_CHANGE);
                _powerSavingStateChangeNotificationHandle = RegisterPowerNotification(hwnd, PInvoke.GUID_POWER_SAVING_STATUS);
            },
            onStopped: () =>
            {
                var kbHookLocal = _kbHook;
                var deviceNotifLocal = _deviceNotificationHandle;
                var consoleDisplayLocal = _consoleDisplayStateNotificationHandle;
                var lidSwitchLocal = _lidSwitchStateChangeNotificationHandle;
                var powerSavingLocal = _powerSavingStateChangeNotificationHandle;
                _kbHook = default;
                _deviceNotificationHandle = default;
                _consoleDisplayStateNotificationHandle = default;
                _lidSwitchStateChangeNotificationHandle = default;
                _powerSavingStateChangeNotificationHandle = default;

                try
                {
                    PInvoke.UnhookWindowsHookEx(kbHookLocal);
                }
                catch (Exception ex)
                {
                    Log.Instance.Warning($"Failed to unhook keyboard hook in NativeWindowsMessageListener: {ex.Message}", ex);
                }

                try
                {
                    PInvoke.UnregisterDeviceNotification(deviceNotifLocal);
                }
                catch (Exception ex)
                {
                    Log.Instance.Warning($"Failed to unregister device notification: {ex.Message}", ex);
                }

                try
                {
                    PInvoke.UnregisterPowerSettingNotification(consoleDisplayLocal);
                }
                catch (Exception ex)
                {
                    Log.Instance.Warning($"Failed to unregister console display state notification: {ex.Message}", ex);
                }

                try
                {
                    PInvoke.UnregisterPowerSettingNotification(lidSwitchLocal);
                }
                catch (Exception ex)
                {
                    Log.Instance.Warning($"Failed to unregister lid switch state notification: {ex.Message}", ex);
                }

                try
                {
                    PInvoke.UnregisterPowerSettingNotification(powerSavingLocal);
                }
                catch (Exception ex)
                {
                    Log.Instance.Warning($"Failed to unregister power saving state notification: {ex.Message}", ex);
                }
            });

        if (!_window.Start(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("Failed to start the native Windows message window within the timeout.");

        return WaitForInit();
    });

    public Task StopAsync() => _mainThreadDispatcher.DispatchAsync(() =>
    {
        _window?.Dispose();
        _window = null;

        return Task.CompletedTask;
    });


    private unsafe LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_DEVICECHANGE && lParam.Value != 0)
        {
            ref var devBroadcastHdr = ref Unsafe.AsRef<DEV_BROADCAST_HDR>((void*)lParam.Value);
            if (devBroadcastHdr.dbch_devicetype == DEV_BROADCAST_HDR_DEVICE_TYPE.DBT_DEVTYP_DEVICEINTERFACE)
            {
                ref var devBroadcastDeviceInterface = ref Unsafe.AsRef<DEV_BROADCAST_DEVICEINTERFACE_W>((void*)lParam.Value);
                var length = ((int)devBroadcastDeviceInterface.dbcc_size - sizeof(DEV_BROADCAST_DEVICEINTERFACE_W)) / sizeof(char);
                var name = devBroadcastDeviceInterface.dbcc_name.AsSpan(length).ToString();

                var state = (uint)wParam.Value;
                switch (state)
                {
                    case PInvoke.DBT_DEVICEARRIVAL:
                        {
                            Log.Instance.Info($"Event received: Device Arrival [name={name}]");

                            OnDeviceConnected(name);
                            break;
                        }
                    case PInvoke.DBT_DEVICEREMOVECOMPLETE:
                        {
                            Log.Instance.Info($"Event received: Device Removal Complete [name={name}]");

                            OnDeviceDisconnected(name);
                            break;
                        }
                }

                if (devBroadcastDeviceInterface.dbcc_classguid == PInvoke.GUID_DISPLAY_DEVICE_ARRIVAL)
                {
                    Log.Instance.Info($"Event received: Display Device Arrival");

                    OnDisplayDeviceArrival();
                }

                if (devBroadcastDeviceInterface.dbcc_classguid == PInvoke.GUID_DEVINTERFACE_MONITOR)
                {
                    var id = InternalDisplay.Get();
                    var isExternal = !name.Equals(id?.DevicePath, StringComparison.Ordinal);

                    switch (state)
                    {
                        case PInvoke.DBT_DEVICEARRIVAL:
                            {
                                Log.Instance.Info($"Event received: Monitor Connected");

                                OnMonitorConnected(isExternal);
                                break;
                            }
                        case PInvoke.DBT_DEVICEREMOVECOMPLETE:
                            {
                                Log.Instance.Info($"Event received: Monitor Disconnected");

                                OnMonitorDisconnected(isExternal);
                                break;
                            }
                    }
                }
            }
        }

        if (msg == PInvoke.WM_POWERBROADCAST && wParam.Value == PInvoke.PBT_POWERSETTINGCHANGE && lParam.Value != 0)
        {
            ref var str = ref Unsafe.AsRef<POWERBROADCAST_SETTING>((void*)lParam.Value);

            if (str.PowerSetting == PInvoke.GUID_CONSOLE_DISPLAY_STATE)
            {
                var state = (PInvokeExtensions.CONSOLE_DISPLAY_STATE)str.Data[0];
                switch (state)
                {
                    case PInvokeExtensions.CONSOLE_DISPLAY_STATE.On:
                        {
                            Log.Instance.Info($"Event received: Monitor On");

                            OnMonitorOn();
                            break;
                        }
                    case PInvokeExtensions.CONSOLE_DISPLAY_STATE.Off:
                        {
                            Log.Instance.Info($"Event received: Monitor Off");

                            OnMonitorOff();
                            break;
                        }
                }
            }

            if (str.PowerSetting == PInvoke.GUID_LIDSWITCH_STATE_CHANGE)
            {
                var isOpened = str.Data[0] != 0;
                if (isOpened)
                {
                    Log.Instance.Info($"Event received: Lid Opened");

                    OnLidOpened();
                }
                else
                {
                    Log.Instance.Info($"Event received: Lid Closed");

                    OnLidClosed();
                }
            }

            if (str.PowerSetting == PInvoke.GUID_POWER_SAVING_STATUS && str.Data[0] == 0)
            {
                Log.Instance.Info($"Event received: Battery Saver enabled");

                OnBatterySaverEnabled();
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private async Task WaitForInit()
    {
        var delayTask = _delayProvider.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
        var task = Task.WhenAll(
            _isMonitorOnTaskCompletionSource.Task,
            _isLidOpenTaskCompletionSource.Task
        );

        var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

        if (completed == delayTask)
        {
            Log.Instance.Warning($"Delay expired, state might be inconsistent! [IsMonitorOn={IsMonitorOn}, IsLidOpen={IsLidOpen}]");
        }
    }

    private void OnMonitorOn()
    {
        IsMonitorOn = true;
        _isMonitorOnTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.MonitorOn);
    }

    private void OnMonitorOff()
    {
        IsMonitorOn = false;
        _isMonitorOnTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.MonitorOff);
    }

    private void OnLidOpened()
    {
        IsLidOpen = true;
        _isLidOpenTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.LidOpened);
    }

    private void OnLidClosed()
    {
        IsLidOpen = false;
        _isLidOpenTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.LidClosed);
    }

    private void OnBatterySaverEnabled()
    {
        Task.Run(() => _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync())
            .Forget("apply Windows power settings after battery saver event");

        RaiseChanged(NativeWindowsMessage.BatterySaverEnabled);
    }

    private void OnDeviceConnected(string name)
    {
        RaiseChanged(NativeWindowsMessage.DeviceConnected, ConvertDeviceNameToDeviceInstanceId(name));
    }

    private void OnDeviceDisconnected(string name)
    {
        RaiseChanged(NativeWindowsMessage.DeviceDisconnected, ConvertDeviceNameToDeviceInstanceId(name));
    }

    private void OnMonitorConnected(bool isExternal)
    {
        RaiseChanged(NativeWindowsMessage.MonitorConnected);

        if (isExternal)
            RaiseChanged(NativeWindowsMessage.ExternalMonitorConnected);
    }

    private void OnMonitorDisconnected(bool isExternal)
    {
        RaiseChanged(NativeWindowsMessage.MonitorDisconnected);

        if (isExternal)
            RaiseChanged(NativeWindowsMessage.ExternalMonitorDisconnected);
    }

    private void OnDisplayDeviceArrival()
    {
        Task.Run(async () =>
        {
            if (await _dgpuNotify.IsSupportedAsync().ConfigureAwait(false))
                await _dgpuNotify.NotifyAsync().ConfigureAwait(false);
        }).Forget("notify discrete GPU after display device arrival");

        RaiseChanged(NativeWindowsMessage.OnDisplayDeviceArrival);
    }

    private void RaiseChanged(NativeWindowsMessage message, object? data = null) => Changed?.Invoke(this, new ChangedEventArgs(message, data));

    private unsafe LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode != PInvoke.HC_ACTION)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        ref var kbStruct = ref Unsafe.AsRef<KBDLLHOOKSTRUCT>((void*)lParam.Value);

        _smartFnLockController.OnKeyboardEvent(wParam.Value, kbStruct);

        if (wParam.Value != PInvoke.WM_KEYUP)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        if (kbStruct.vkCode == (ulong)VIRTUAL_KEY.VK_CAPITAL)
        {
            var isOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 0x1) != 0;
            var type = isOn ? NotificationType.CapsLockOn : NotificationType.CapsLockOff;
            MessagingCenter.Publish(new NotificationMessage(type));
        }

        if (kbStruct.vkCode == (ulong)VIRTUAL_KEY.VK_NUMLOCK)
        {
            var isOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_NUMLOCK) & 0x1) != 0;
            var type = isOn ? NotificationType.NumLockOn : NotificationType.NumLockOff;
            MessagingCenter.Publish(new NotificationMessage(type));
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    private static unsafe HDEVNOTIFY RegisterDeviceNotification(HWND hwnd)
    {
        var ptr = IntPtr.Zero;
        try
        {
            var str = new DEV_BROADCAST_DEVICEINTERFACE_W();
            str.dbcc_size = (uint)Marshal.SizeOf(str);
            str.dbcc_devicetype = (uint)DEV_BROADCAST_HDR_DEVICE_TYPE.DBT_DEVTYP_DEVICEINTERFACE;
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(str));
            Marshal.StructureToPtr(str, ptr, true);
            return PInvoke.RegisterDeviceNotification(new HANDLE(hwnd.Value),
                ptr.ToPointer(),
                REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE | REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_ALL_INTERFACE_CLASSES);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static unsafe HPOWERNOTIFY RegisterPowerNotification(HWND hwnd, Guid guid)
    {
        return PInvoke.RegisterPowerSettingNotification(new HANDLE(hwnd.Value), &guid, 0);
    }

    private static string? ConvertDeviceNameToDeviceInstanceId(string name)
    {
        var parts = name.Split('#');
        if (parts.Length < 3)
            return null;

        var part1 = parts[0].TrimStart('\\', '?');
        var part2 = parts[1].Replace('#', '\\');
        var part3 = parts[2];
        return $@"{part1}\{part2}\{part3}".ToUpperInvariant();
    }
}
