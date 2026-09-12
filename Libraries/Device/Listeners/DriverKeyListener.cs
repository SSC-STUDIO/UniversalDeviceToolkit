using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;

namespace UniversalDeviceToolkit.Lib.Listeners;

public class DriverKeyListener(
    FnKeysDisabler fnKeysDisabler,
    MicrophoneFeature microphoneFeature,
    TouchpadLockFeature touchpadLockFeature,
    WhiteKeyboardBacklightFeature whiteKeyboardBacklightFeature)
    : IListener<DriverKeyListener.ChangedEventArgs>
{
    public class ChangedEventArgs(DriverKey driverKey) : EventArgs
    {
        public DriverKey DriverKey { get; } = driverKey;
    }

    public event EventHandler<ChangedEventArgs>? Changed;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenTask;

    public Task StartAsync()
    {
        if (_listenTask is not null)
            return Task.CompletedTask;

        _cancellationTokenSource = new();
        _listenTask = Task.Run(() => HandlerAsync(_cancellationTokenSource.Token));

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        var cts = _cancellationTokenSource;
        _cancellationTokenSource = null;

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            cts.Dispose();
        }

        if (_listenTask is not null)
            await _listenTask.ConfigureAwait(false);

        _listenTask = null;
    }

    private async Task HandlerAsync(CancellationToken token)
    {
        try
        {
            using var resetEvent = new ManualResetEvent(false);
            var setHandleResult = BindListener(resetEvent);
            if (!setHandleResult)
                PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, setHandleResult");

            GetValue(out _); // Clear register

            while (true)
            {
                WaitHandle.WaitAny([resetEvent, token.WaitHandle]);

                token.ThrowIfCancellationRequested();

                if (await fnKeysDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ignoring, FnKeys are enabled.");

                    resetEvent.Reset();
                    continue;
                }

                var getValueResult = GetValue(out var value);
                if (!getValueResult)
                    PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, getValueResult");

                var key = (DriverKey)value;
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Event received. [key={key}, value={value}]");

                await OnChangedAsync(key).ConfigureAwait(false);
                Changed?.Invoke(this, new(key));

                resetEvent.Reset();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when listener is cancelled, no action needed
        }
        catch (ThreadAbortException)
        {
            // Expected when thread is aborted, no action needed
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Unknown error.", ex);
        }
    }

    private async Task OnChangedAsync(DriverKey value)
    {
        try
        {
            if (value.HasFlag(DriverKey.FnF4))
            {
                if (await microphoneFeature.IsSupportedAsync().ConfigureAwait(false))
                {
                    switch (await microphoneFeature.GetStateAsync().ConfigureAwait(false))
                    {
                        case MicrophoneState.On:
                            await microphoneFeature.SetStateAsync(MicrophoneState.Off).ConfigureAwait(false);
                            MessagingCenter.Publish(new NotificationMessage(NotificationType.MicrophoneOff));
                            break;
                        case MicrophoneState.Off:
                            await microphoneFeature.SetStateAsync(MicrophoneState.On).ConfigureAwait(false);
                            MessagingCenter.Publish(new NotificationMessage(NotificationType.MicrophoneOn));
                            break;
                    }
                }
            }

            if (value.HasFlag(DriverKey.FnF8))
                AirplaneMode.Open();

            if (value.HasFlag(DriverKey.FnF10))
            {
                if (await touchpadLockFeature.IsSupportedAsync().ConfigureAwait(false))
                {
                    var status = await touchpadLockFeature.GetStateAsync().ConfigureAwait(false);
                    MessagingCenter.Publish(status == TouchpadLockState.Off
                        ? new NotificationMessage(NotificationType.TouchpadOn)
                        : new NotificationMessage(NotificationType.TouchpadOff));
                }
            }

            if (value.HasFlag(DriverKey.FnSpace))
            {
                if (await whiteKeyboardBacklightFeature.IsSupportedAsync().ConfigureAwait(false))
                {
                    var state = await whiteKeyboardBacklightFeature.GetStateAsync().ConfigureAwait(false);
                    MessagingCenter.Publish(state == WhiteKeyboardBacklightState.Off
                        ? new NotificationMessage(NotificationType.WhiteKeyboardBacklightOff, state.GetDisplayName())
                        : new NotificationMessage(NotificationType.WhiteKeyboardBacklightChanged, state.GetDisplayName()));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Couldn't handle key press. [value={value}]", ex);
        }
    }

    private static unsafe bool BindListener(WaitHandle waitHandle)
    {
        var handle = (uint)waitHandle.SafeWaitHandle.DangerousGetHandle();
        return PInvoke.DeviceIoControl(Drivers.GetEnergy().ToWin32Handle(),
            Drivers.IOCTL_KEY_WAIT_HANDLE,
            &handle,
            16,
            null,
            0,
            null,
            null);
    }

    private static unsafe bool GetValue(out uint value)
    {
        uint inBuff = 0;
        uint outBuff = 0;
        var result = PInvoke.DeviceIoControl(Drivers.GetEnergy().ToWin32Handle(),
            Drivers.IOCTL_KEY_VALUE,
            &inBuff,
            4,
            &outBuff,
            4,
            null,
            null);
        value = outBuff;
        return result;
    }
}
