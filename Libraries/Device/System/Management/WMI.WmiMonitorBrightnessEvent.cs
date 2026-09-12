using System;
using System.Management;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class WmiMonitorBrightnessEvent
    {
        public static IDisposable Listen(Action<byte> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM WmiMonitorBrightnessEvent",
            ConvertAndHandle(handler));

        public static Task<IDisposable> ListenAsync(Action<byte> handler) => WMI.ListenAsync("root\\WMI",
            $"SELECT * FROM WmiMonitorBrightnessEvent",
            ConvertAndHandle(handler));

        private static Action<PropertyDataCollection> ConvertAndHandle(Action<byte> handler) =>
            pdc =>
            {
                var value = Convert.ToByte(pdc["Brightness"].Value);
                handler(value);
            };
    }
}
