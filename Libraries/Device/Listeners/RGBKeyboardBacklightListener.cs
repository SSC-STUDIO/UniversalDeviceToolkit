using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Listeners;

public class RGBKeyboardBacklightListener(RGBKeyboardBacklightController controller)
    : AbstractWMIListener<EventArgs, RGBKeyboardBacklightChanged, int>(WMI.LenovoGameZoneLightProfileChangeEvent.ListenAsync)
{
    protected override RGBKeyboardBacklightChanged GetValue(int value) => default;

    protected override EventArgs GetEventArgs(RGBKeyboardBacklightChanged value) => EventArgs.Empty;

    protected override async Task OnChangedAsync(RGBKeyboardBacklightChanged value)
    {
        try
        {
            if (!await controller.IsSupportedAsync().ConfigureAwait(false))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Not supported.");

                return;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Taking ownership...");

            await controller.SetLightControlOwnerAsync(true).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting next preset set...");

            var preset = await controller.SetNextPresetAsync().ConfigureAwait(false);

            MessagingCenter.Publish(preset == RGBKeyboardBacklightPreset.Off
                ? new NotificationMessage(NotificationType.RGBKeyboardBacklightOff, preset.GetDisplayName())
                : new NotificationMessage(NotificationType.RGBKeyboardBacklightChanged, preset.GetDisplayName()));

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Next preset set");
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Failed to set next keyboard backlight preset.", ex);
        }
    }
}
