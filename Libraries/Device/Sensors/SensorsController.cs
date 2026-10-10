using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public class SensorsController(
    SensorsControllerV1 controllerV1,
    SensorsControllerV2 controllerV2,
    SensorsControllerV3 controllerV3,
    SensorsControllerV4 controllerV4,
    SensorsControllerV5 controllerV5,
    AsusSensorsController asusController,
    HpSensorsController hpController,
    RazerSensorsController razerController,
    AlienwareSensorsController alienwareController,
    AcerSensorsController acerController,
    GigabyteSensorsController gigabyteController,
    MsiSensorsController msiController,
    TongfangSensorsController tongfangController,
    ClevoSensorsController clevoController,
    GenericSensorsController genericController)
    : ISensorsController
{
    private ISensorsController? _controller;

    public async Task<bool> IsSupportedAsync() => await GetControllerAsync().ConfigureAwait(false) is not null;

    public async Task PrepareAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false) ?? throw ExceptionHelper.NoSupportedControllerFound();
        await controller.PrepareAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        // No resources to dispose directly, injected controllers are managed by IoC
        GC.SuppressFinalize(this);
    }

    public async Task<SensorsData> GetDataAsync(bool detailed = false)
    {
        var controller = await GetControllerAsync().ConfigureAwait(false) ?? throw ExceptionHelper.NoSupportedControllerFound();
        return await controller.GetDataAsync(detailed).ConfigureAwait(false);
    }

    public async Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false) ?? throw ExceptionHelper.NoSupportedControllerFound();
        return await controller.GetFanSpeedsAsync().ConfigureAwait(false);
    }

    private async Task<ISensorsController?> GetControllerAsync()
    {
        if (_controller is not null)
            return _controller;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace("Selecting sensors controller...");

        if (await ProbeControllerAsync(controllerV5).ConfigureAwait(false))
            return _controller = controllerV5;

        if (await ProbeControllerAsync(controllerV4).ConfigureAwait(false))
            return _controller = controllerV4;

        if (await ProbeControllerAsync(controllerV3).ConfigureAwait(false))
            return _controller = controllerV3;

        if (await ProbeControllerAsync(controllerV2).ConfigureAwait(false))
            return _controller = controllerV2;

        if (await ProbeControllerAsync(controllerV1).ConfigureAwait(false))
            return _controller = controllerV1;

        if (await ProbeControllerAsync(asusController).ConfigureAwait(false))
            return _controller = asusController;

        if (await ProbeControllerAsync(hpController).ConfigureAwait(false))
            return _controller = hpController;

        if (await ProbeControllerAsync(razerController).ConfigureAwait(false))
            return _controller = razerController;

        if (await ProbeControllerAsync(alienwareController).ConfigureAwait(false))
            return _controller = alienwareController;

        if (await ProbeControllerAsync(acerController).ConfigureAwait(false))
            return _controller = acerController;

        if (await ProbeControllerAsync(gigabyteController).ConfigureAwait(false))
            return _controller = gigabyteController;

        if (await ProbeControllerAsync(msiController).ConfigureAwait(false))
            return _controller = msiController;

        if (await ProbeControllerAsync(tongfangController).ConfigureAwait(false))
            return _controller = tongfangController;

        if (await ProbeControllerAsync(clevoController).ConfigureAwait(false))
            return _controller = clevoController;

        if (await ProbeControllerAsync(genericController).ConfigureAwait(false))
            return _controller = genericController;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace("No supported sensors controller found.");

        return null;
    }

    private static async Task<bool> ProbeControllerAsync(ISensorsController controller)
    {
        var supported = await controller.IsSupportedAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Sensors controller probe result: {supported}. [type={controller.GetType().Name}]");

        return supported;
    }
}
