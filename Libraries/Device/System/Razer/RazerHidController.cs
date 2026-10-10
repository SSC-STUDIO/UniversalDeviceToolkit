using System;
using System.Threading;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Razer;

/// <summary>Raw HID access used by <see cref="RazerHidController"/>; fakeable for tests.</summary>
public interface IRazerHid
{
    string[] EnumerateDevicePaths(ushort vendorId);
    bool GetVidPid(string devicePath, out ushort vendorId, out ushort productId);
    bool TrySendFeatureReport(string devicePath, byte[] report);
    bool TryGetFeatureReport(string devicePath, byte[] report);
}

/// <summary>
/// High-level Razer Blade EC controller: locates the HID control interface with
/// a benign probe, then exchanges class-0x0D commands with response validation
/// and retries (protocol per openrazer / razer-laptop-control lineage).
/// </summary>
public interface IRazerHidController
{
    bool Probe();

    /// <summary>Raw performance-mode value for a zone, or null when the EC did not answer.</summary>
    int? GetPerformanceMode(byte zone);

    bool SetPerformanceMode(byte zone, byte mode, bool manualFan);

    /// <summary>Fan RPM for a zone (value × 100), or null when unavailable.</summary>
    int? GetFanRpm(byte zone);
}

public sealed class RazerHidController(IRazerHid hid) : IRazerHidController
{
    public const ushort RazerVendorId = 0x1532;
    private const int MaxAttempts = 3;

    private string? _controlPath;

    public bool Probe()
    {
        if (_controlPath is not null)
            return true;

        try
        {
            foreach (var path in hid.EnumerateDevicePaths(RazerVendorId))
            {
                if (!hid.GetVidPid(path, out var vid, out _))
                    continue;
                if (vid != RazerVendorId)
                    continue;

                // Benign probe (perf-mode GET) — the first collection that answers
                // with a valid echo is the EC control interface.
                if (TryExchange(path, RazerPacket.ClassPerformance, RazerPacket.CmdGetPerformanceMode,
                        [0x00, RazerPacket.ZoneCpu, 0x00, 0x00], out _))
                {
                    _controlPath = path;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Razer HID enumeration failed.", ex);
        }

        return false;
    }

    public int? GetPerformanceMode(byte zone)
    {
        if (!Probe())
            return null;

        if (!TryExchange(_controlPath!, RazerPacket.ClassPerformance, RazerPacket.CmdGetPerformanceMode,
                [0x00, zone, 0x00, 0x00], out var response))
            return null;

        return RazerPacket.GetArgument(response, 2);
    }

    public bool SetPerformanceMode(byte zone, byte mode, bool manualFan)
    {
        if (!Probe())
            return false;

        return TryExchange(_controlPath!, RazerPacket.ClassPerformance, RazerPacket.CmdSetPerformanceMode,
            [0x00, zone, mode, manualFan ? (byte)0x01 : (byte)0x00], out _);
    }

    public int? GetFanRpm(byte zone)
    {
        if (!Probe())
            return null;

        if (!TryExchange(_controlPath!, RazerPacket.ClassPerformance, RazerPacket.CmdGetFanRpm,
                [0x00, zone, 0x00], out var response))
            return null;

        var level = RazerPacket.GetArgument(response, 2);
        return level == 0 ? null : level * 100;
    }

    private bool TryExchange(string path, byte commandClass, byte commandId, byte[] arguments, out byte[] response)
    {
        response = [];

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                var report = RazerPacket.BuildReport(commandClass, commandId, arguments);
                if (!hid.TrySendFeatureReport(path, report))
                {
                    Pause();
                    continue;
                }

                var read = (byte[])report.Clone();
                if (!hid.TryGetFeatureReport(path, read))
                {
                    Pause();
                    continue;
                }

                if (RazerPacket.IsValidResponse(read, commandClass, commandId))
                {
                    response = read;
                    return true;
                }

                Pause();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Razer HID exchange failed. [class=0x{commandClass:X2}, cmd=0x{commandId:X2}]", ex);
                Pause();
            }
        }

        return false;
    }

    private static void Pause() => Thread.Sleep(2); // EC drops commands sent too fast
}
