using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Lib;

public readonly struct FanTableData(FanTableType type, byte fanId, byte sensorId, ushort[] fanSpeeds, ushort[] temps)
{
    public FanTableType Type { get; } = type;
    public byte FanId { get; } = fanId;
    public byte SensorId { get; } = sensorId;
    public ushort[] FanSpeeds { get; } = fanSpeeds;
    public ushort[] Temps { get; } = temps;

    public override string ToString() =>
        $"{nameof(Type)}: {Type}," +
        $" {nameof(FanId)}: {FanId}," +
        $" {nameof(SensorId)}: {SensorId}," +
        $" {nameof(FanSpeeds)}: [{string.Join(", ", FanSpeeds)}]," +
        $" {nameof(Temps)}: [{string.Join(", ", Temps)}]";
}

public readonly struct FanTable
{
    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable IdentifierTypo
    // ReSharper disable InconsistentNaming

    public byte FSTM { get; init; }
    public byte FSID { get; init; }
    public uint FSTL { get; init; }
    public ushort FSS0 { get; init; }
    public ushort FSS1 { get; init; }
    public ushort FSS2 { get; init; }
    public ushort FSS3 { get; init; }
    public ushort FSS4 { get; init; }
    public ushort FSS5 { get; init; }
    public ushort FSS6 { get; init; }
    public ushort FSS7 { get; init; }
    public ushort FSS8 { get; init; }
    public ushort FSS9 { get; init; }

    // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
    // ReSharper restore MemberCanBePrivate.Global
    // ReSharper restore IdentifierTypo
    // ReSharper restore InconsistentNaming

    public FanTable(ushort[] fanTable)
    {
        if (fanTable.Length != 10)
            // ReSharper disable once LocalizableElement
            throw ExceptionHelper.FanTableLength(nameof(fanTable));

        FSTM = 1;
        FSID = 0;
        FSTL = 0;
        FSS0 = fanTable[0];
        FSS1 = fanTable[1];
        FSS2 = fanTable[2];
        FSS3 = fanTable[3];
        FSS4 = fanTable[4];
        FSS5 = fanTable[5];
        FSS6 = fanTable[6];
        FSS7 = fanTable[7];
        FSS8 = fanTable[8];
        FSS9 = fanTable[9];
    }

    public ushort[] GetTable() => [FSS0, FSS1, FSS2, FSS3, FSS4, FSS5, FSS6, FSS7, FSS8, FSS9];

    public byte[] GetBytes()
    {
        using var ms = new MemoryStream(new byte[64]);
        ms.WriteByte(FSTM);
        ms.WriteByte(FSID);
        ms.Write(BitConverter.GetBytes(FSTL));
        ms.Write(BitConverter.GetBytes(FSS0));
        ms.Write(BitConverter.GetBytes(FSS1));
        ms.Write(BitConverter.GetBytes(FSS2));
        ms.Write(BitConverter.GetBytes(FSS3));
        ms.Write(BitConverter.GetBytes(FSS4));
        ms.Write(BitConverter.GetBytes(FSS5));
        ms.Write(BitConverter.GetBytes(FSS6));
        ms.Write(BitConverter.GetBytes(FSS7));
        ms.Write(BitConverter.GetBytes(FSS8));
        ms.Write(BitConverter.GetBytes(FSS9));
        return ms.ToArray();
    }

    public override string ToString() =>
        $"{nameof(FSTM)}: {FSTM}," +
        $" {nameof(FSID)}: {FSID}," +
        $" {nameof(FSTL)}: {FSTL}," +
        $" {nameof(FSS0)}: {FSS0}," +
        $" {nameof(FSS1)}: {FSS1}," +
        $" {nameof(FSS2)}: {FSS2}," +
        $" {nameof(FSS3)}: {FSS3}," +
        $" {nameof(FSS4)}: {FSS4}," +
        $" {nameof(FSS5)}: {FSS5}," +
        $" {nameof(FSS6)}: {FSS6}," +
        $" {nameof(FSS7)}: {FSS7}," +
        $" {nameof(FSS8)}: {FSS8}," +
        $" {nameof(FSS9)}: {FSS9}";
}

public readonly struct FanTableInfo(FanTableData[] data, FanTable table)
{
    public FanTableData[] Data { get; } = data;
    public FanTable Table { get; } = table;

    public override string ToString() =>
        $"{nameof(Data)}: [{string.Join(", ", Data)}]," +
        $" {nameof(Table)}: {Table}";
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct CIntelligentCooling
{
    private long _value;
}

public readonly struct FanSpeedTable(int cpuFanSpeed, int gpuFanSpeed, int pchFanSpeed)
{
    public int CpuFanSpeed { get; } = cpuFanSpeed;
    public int GpuFanSpeed { get; } = gpuFanSpeed;
    public int PchFanSpeed { get; } = pchFanSpeed;
}
