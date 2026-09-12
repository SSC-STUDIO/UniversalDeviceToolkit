using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public class HardwareSensorsFeature(ApplicationSettings settings, OsdSettings osdSettings, SensorsGroupController sensorsGroupController) : IFeature<HardwareSensorsState>
{
    public Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default) => Task.FromResult(PawnIOHelper.IsPawnIOInstalled());

    public Task<HardwareSensorsState[]> GetAllStatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enum.GetValues<HardwareSensorsState>());

    public Task<HardwareSensorsState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var state = settings.Store.EnableHardwareSensors
            ? HardwareSensorsState.On
            : HardwareSensorsState.Off;
        return Task.FromResult(state);
    }

    public async Task SetStateAsync(HardwareSensorsState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state == HardwareSensorsState.On && !sensorsGroupController.IsLibreHardwareMonitorInitialized())
            await sensorsGroupController.IsSupportedAsync().ConfigureAwait(false);

        if (state == HardwareSensorsState.Off)
        {
            osdSettings.Store.ShowOsd = false;
            osdSettings.SynchronizeStore();
            MessagingCenter.Publish(new OsdChangedMessage(OsdState.Hidden));
        }

        settings.Store.EnableHardwareSensors = state == HardwareSensorsState.On;
        settings.SynchronizeStore();
    }

    public void InvalidateResolution()
    {
    }
}
