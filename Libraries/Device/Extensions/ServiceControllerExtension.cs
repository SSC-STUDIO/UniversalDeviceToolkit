using System.ServiceProcess;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.System.Services;

namespace UniversalDeviceToolkit.Lib.Extensions;

internal static class ServiceControllerExtension
{
    public static unsafe void ChangeStartMode(this ServiceController svc, bool enabled)
    {
        using var scManagerHandle = PInvoke.OpenSCManager(null as string, null, PInvoke.SC_MANAGER_ALL_ACCESS);
        if (scManagerHandle.IsInvalid)
            throw ExceptionHelper.OpenServiceManagerError();

        using var serviceHandle = PInvoke.OpenService(scManagerHandle, svc.ServiceName, PInvoke.SERVICE_CHANGE_CONFIG);
        if (serviceHandle.IsInvalid)
            throw ExceptionHelper.OpenServiceError();

        var result = PInvoke.ChangeServiceConfig(serviceHandle,
            (ENUM_SERVICE_TYPE)PInvoke.SERVICE_NO_CHANGE,
            enabled ? SERVICE_START_TYPE.SERVICE_AUTO_START : SERVICE_START_TYPE.SERVICE_DISABLED,
            SERVICE_ERROR.SERVICE_ERROR_NORMAL,
            null,
            null,
            out _,
            null,
            null,
            null,
            null);

        if (result)
            return;

        PInvokeExtensions.ThrowIfWin32Error($"Could not change service: {svc.ServiceName}");
    }
}
