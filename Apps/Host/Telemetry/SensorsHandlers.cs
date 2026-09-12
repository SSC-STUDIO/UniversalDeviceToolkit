using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Sensor bridge: LibreHardwareMonitor snapshot + subscription, vendor fallback,
/// FPS monitoring and sensor-related settings.
/// </summary>
public static partial class SensorsHandlers
{

    public static void Register(BridgeRpcServer rpc)
    {
        EnsureUiActivityHook();
        rpc.RegisterHandler("sensors.getStatus", (_, ct) => HandleGetStatusAsync(ct));
        rpc.RegisterHandler("sensors.getSnapshot", (_, ct) => HandleGetSnapshotAsync(ct));
        rpc.RegisterHandler("sensors.getDetailed", (_, ct) => HandleGetDetailedAsync(ct));
        rpc.RegisterHandler("sensors.subscribe", (request, ct) => HandleSubscribeAsync(request, rpc, ct));
        rpc.RegisterHandler("sensors.unsubscribe", (request, ct) => HandleUnsubscribeAsync(request, ct));
        rpc.RegisterHandler("sensors.getSettings", (_, ct) => HandleGetSettingsAsync(ct));
        rpc.RegisterHandler("sensors.setSettings", (request, ct) => HandleSetSettingsAsync(request, rpc, ct));
        rpc.RegisterHandler("sensors.getFps", (_, ct) => HandleGetFpsAsync(ct));
        rpc.RegisterHandler("sensors.subscribeFps", (request, ct) => HandleSubscribeFpsAsync(request, rpc, ct));
        rpc.RegisterHandler("sensors.unsubscribeFps", (_, ct) => HandleUnsubscribeFpsAsync(ct));
    }
}
