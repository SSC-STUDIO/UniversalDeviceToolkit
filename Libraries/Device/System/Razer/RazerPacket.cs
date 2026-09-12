using System;

namespace UniversalDeviceToolkit.Lib.System.Razer;

/// <summary>
/// Razer laptop EC report protocol (mirrors openrazer's razercommon.h and the
/// razer-laptop-control lineage): 90-byte packets carried in a 91-byte HID
/// Feature report (leading report-id byte 0x00). All helpers are pure so the
/// protocol logic is testable without hardware.
/// </summary>
public static class RazerPacket
{
    public const int ReportLength = 91;
    public const int PacketLength = 90;
    public const byte StatusNew = 0x00;
    public const byte StatusSuccessful = 0x02;
    public const byte TransactionId = 0x1F;

    public const byte ClassPerformance = 0x0D;
    public const byte CmdSetFanRpm = 0x01;
    public const byte CmdSetPerformanceMode = 0x02;
    public const byte CmdGetFanRpm = 0x81;
    public const byte CmdGetPerformanceMode = 0x82;

    public const byte ZoneCpu = 0x01;
    public const byte ZoneGpu = 0x02;

    /// <summary>Builds a 91-byte feature report (report id 0 + 90-byte packet) with CRC over packet bytes 2..87.</summary>
    public static byte[] BuildReport(byte commandClass, byte commandId, ReadOnlySpan<byte> arguments)
    {
        if (arguments.Length > 80)
            throw new ArgumentOutOfRangeException(nameof(arguments));

        var report = new byte[ReportLength];
        report[0] = 0x00; // report id

        // Packet starts at index 1.
        report[1] = StatusNew;
        report[2] = TransactionId;
        report[3] = 0x00; // remaining packets (big-endian u16) = 0
        report[4] = 0x00;
        report[5] = 0x00; // protocol type
        report[6] = (byte)arguments.Length;
        report[7] = commandClass;
        report[8] = commandId;

        for (var i = 0; i < arguments.Length; i++)
            report[9 + i] = arguments[i];

        // CRC = XOR of packet bytes 2..87 (indexes 2..87 of the 90-byte packet).
        byte crc = 0;
        for (var i = 2; i < 88; i++)
            crc ^= report[1 + i];
        report[1 + 88] = crc;
        report[1 + 89] = 0x00; // reserved

        return report;
    }

    /// <summary>Validates a response report per rnd-ash/librazer rules: status 0x02 and class/id echoing the request.</summary>
    public static bool IsValidResponse(ReadOnlySpan<byte> response, byte expectedClass, byte expectedCommandId)
    {
        if (response.Length < ReportLength)
            return false;

        return response[1] == StatusSuccessful &&
               response[7] == expectedClass &&
               response[8] == expectedCommandId;
    }

    /// <summary>Response payload (arguments) accessor: packet args start at index 9.</summary>
    public static byte GetArgument(ReadOnlySpan<byte> response, int index)
    {
        var packetIndex = 9 + index;
        return packetIndex < response.Length ? response[packetIndex] : (byte)0;
    }
}
