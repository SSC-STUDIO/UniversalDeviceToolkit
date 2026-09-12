using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.Listeners;

public class WinKeyListener()
    : AbstractWMIListener<EventArgs, WinKeyChanged, int>(WMI.LenovoGameZoneKeyLockStatusEvent.ListenAsync)
{
    protected override WinKeyChanged GetValue(int value) => default;

    protected override EventArgs GetEventArgs(WinKeyChanged value) => EventArgs.Empty;

    protected override Task OnChangedAsync(WinKeyChanged value) => Task.CompletedTask;
}
