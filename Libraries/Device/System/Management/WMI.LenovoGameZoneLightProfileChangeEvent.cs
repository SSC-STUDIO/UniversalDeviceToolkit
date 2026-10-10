using System;
using System.Management;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class LenovoGameZoneLightProfileChangeEvent
    {
        public static IDisposable Listen(Action<int> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_LIGHT_PROFILE_CHANGE_EVENT",
            ConvertAndHandle(handler));

        public static Task<IDisposable> ListenAsync(Action<int> handler) => WMI.ListenAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_LIGHT_PROFILE_CHANGE_EVENT",
            ConvertAndHandle(handler));

        private static Action<PropertyDataCollection> ConvertAndHandle(Action<int> handler) =>
            pdc =>
            {
                var value = Convert.ToInt32(pdc["EventId"].Value);
                handler(value);
            };
    }
}
