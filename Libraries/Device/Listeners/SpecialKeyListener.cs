using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Listeners;

public class SpecialKeyListener(
    ApplicationSettings settings,
    FnKeysDisabler fnKeysDisabler,
    RefreshRateFeature feature,
    MicrophoneFeature microphoneFeature,
    IDelayProvider? delayProvider = null)
    : AbstractWMIListener<SpecialKeyListener.ChangedEventArgs, SpecialKey, int>(WMI.LenovoUtilityEvent.ListenAsync)
{
    private readonly IDelayProvider _delayProvider = delayProvider ?? new DefaultDelayProvider();
    public class ChangedEventArgs(SpecialKey specialKey) : EventArgs
    {
        public SpecialKey SpecialKey { get; } = specialKey;
    }

    private readonly ThrottleFirstDispatcher _refreshRateDispatcher = new(TimeSpan.FromSeconds(2), nameof(SpecialKeyListener));

    protected override SpecialKey GetValue(int value)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received. [value={value}]");

        var result = (SpecialKey)value;
        return result;
    }

    protected override ChangedEventArgs GetEventArgs(SpecialKey value) => new(value);

    protected override async Task OnChangedAsync(SpecialKey value)
    {
        try
        {
            if (await fnKeysDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ignoring, FnKeys are enabled.");
                return;
            }

            switch (value)
            {
                case SpecialKey.CameraOn or SpecialKey.CameraOff:
                    NotifyCameraState(value);
                    break;
                case SpecialKey.FnLockOn or SpecialKey.FnLockOff:
                    NotifyFnLockState(value);
                    break;
                case SpecialKey.FnR or SpecialKey.FnR2:
                    await ToggleRefreshRateAsync().ConfigureAwait(false);
                    break;
                case SpecialKey.FnPrtSc or SpecialKey.FnPrtSc2:
                    OpenSnippingTool();
                    break;
                case SpecialKey.SpectrumBacklightOff:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.Off);
                    break;
                case SpecialKey.SpectrumBacklight1:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.Low);
                    break;
                case SpecialKey.SpectrumBacklight2:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.Medium);
                    break;
                case SpecialKey.SpectrumBacklight3:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.High);
                    break;
                case SpecialKey.SpectrumPreset1:
                    NotifySpectrumPreset(1);
                    break;
                case SpecialKey.SpectrumPreset2:
                    NotifySpectrumPreset(2);
                    break;
                case SpecialKey.SpectrumPreset3:
                    NotifySpectrumPreset(3);
                    break;
                case SpecialKey.SpectrumPreset4:
                    NotifySpectrumPreset(4);
                    break;
                case SpecialKey.SpectrumPreset5:
                    NotifySpectrumPreset(5);
                    break;
                case SpecialKey.SpectrumPreset6:
                    NotifySpectrumPreset(6);
                    break;
                case SpecialKey.FnF4:
                    await ToggleMicrophoneAsync().ConfigureAwait(false);
                    break;
                case SpecialKey.FnF8:
                    OpenAirplaneModeSettings();
                    break;
                case SpecialKey.WhiteBacklightOff:
                    NotifyWhiteBacklight(WhiteKeyboardBacklightState.Off);
                    break;
                case SpecialKey.WhiteBacklight1:
                    NotifyWhiteBacklight(WhiteKeyboardBacklightState.Low);
                    break;
                case SpecialKey.WhiteBacklight2:
                    NotifyWhiteBacklight(WhiteKeyboardBacklightState.High);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Failed to handle key. [key={value}, value={(int)value}]", ex);
        }
    }

    private static void NotifyCameraState(SpecialKey value)
    {
        switch (value)
        {
            case SpecialKey.CameraOn:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.CameraOn));
                break;
            case SpecialKey.CameraOff:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.CameraOff));
                break;
        }
    }

    private static void NotifyFnLockState(SpecialKey value)
    {
        switch (value)
        {
            case SpecialKey.FnLockOn:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.FnLockOn));
                break;
            case SpecialKey.FnLockOff:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.FnLockOff));
                break;
        }
    }

    private Task ToggleRefreshRateAsync() => _refreshRateDispatcher.DispatchAsync(async () =>
    {
        try
        {
            if (!await feature.IsSupportedAsync().ConfigureAwait(false))
                return;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switch refresh rate after Fn+R...");

            var all = await feature.GetAllStatesAsync().ConfigureAwait(false);
            var current = await feature.GetStateAsync().ConfigureAwait(false);
            var excluded = settings.Store.ExcludedRefreshRates;

            var filtered = all.Except(excluded).ToArray();

            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace($"Refresh rates: [all={string.Join(", ", all.Select(r => r.Frequency))}]");
                Log.Instance.Trace($" - All: {string.Join(", ", all.Select(r => r.Frequency))}");
                Log.Instance.Trace($" - Excluded: {string.Join(", ", excluded.Select(r => r.Frequency))}");
                Log.Instance.Trace($" - Filtered: {string.Join(", ", filtered.Select(r => r.Frequency))}");
            }

            if (filtered.Length < 2)
            {
                Log.Instance.Warning($"Can't switch refresh rate after Fn+R when there is less than 2 available.");
                return;
            }

            var currentIndex = Array.IndexOf(filtered, current);
            var newIndex = currentIndex + 1;
            if (newIndex >= filtered.Length)
                newIndex = 0;

            var next = filtered[newIndex];

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switching refresh rate after Fn+R to {next}...");

            await feature.SetStateAsync(next).ConfigureAwait(false);

            _ = _delayProvider.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ContinueWith(_ =>
            {
                MessagingCenter.Publish(new NotificationMessage(NotificationType.RefreshRate, next.DisplayName));
            });

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switched refresh rate after Fn+R to {next}.");
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Failed to switch refresh rate after Fn+R.", ex);
        }
    });

    private static void OpenSnippingTool()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting snipping tool..");

        using var process = Process.Start("explorer", "ms-screenclip:");
    }

    private static void NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness value)
    {
        // LED/toast feedback is isolated: failures must not prevent key handling upstream.
        SpecialKeyLedIsolation.RunLedFeedback($"spectrum-backlight-{value}", () =>
        {
            var type = value is SpectrumKeyboardBacklightBrightness.Off
                ? NotificationType.SpectrumBacklightOff
                : NotificationType.SpectrumBacklightChanged;
            MessagingCenter.Publish(new NotificationMessage(type, value));
        });
    }

    private static void NotifySpectrumPreset(int value) =>
        SpecialKeyLedIsolation.RunLedFeedback($"spectrum-preset-{value}",
            () => MessagingCenter.Publish(new NotificationMessage(NotificationType.SpectrumBacklightPresetChanged, value)));

    private static void NotifyWhiteBacklight(WhiteKeyboardBacklightState value)
    {
        SpecialKeyLedIsolation.RunLedFeedback($"white-backlight-{value}", () =>
        {
            var type = value is WhiteKeyboardBacklightState.Off
                ? NotificationType.WhiteKeyboardBacklightOff
                : NotificationType.WhiteKeyboardBacklightChanged;
            MessagingCenter.Publish(new NotificationMessage(type, value));
        });
    }

    private async Task ToggleMicrophoneAsync()
    {

        if (!await microphoneFeature.IsSupportedAsync().ConfigureAwait(false))
            return;

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

    private static void OpenAirplaneModeSettings() => AirplaneMode.Open();
}
