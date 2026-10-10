using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib;

public readonly struct SensorData(
    int utilization,
    int maxUtilization,
    int coreClock,
    int maxCoreClock,
    int memoryClock,
    int maxMemoryClock,
    int temperature,
    int maxTemperature,
    int wattage,
    double voltage,
    int fanSpeed,
    int maxFanSpeed)
{
    public static readonly SensorData Empty = new(-1, -1, -1, -1, -1, -1, -1, -1, -1, 0, -1, -1);

    public SensorData(
        int utilization,
        int maxUtilization,
        int coreClock,
        int maxCoreClock,
        int memoryClock,
        int maxMemoryClock,
        int temperature,
        int maxTemperature,
        int fanSpeed,
        int maxFanSpeed)
        : this(utilization, maxUtilization, coreClock, maxCoreClock, memoryClock, maxMemoryClock, temperature, maxTemperature, -1, 0, fanSpeed, maxFanSpeed)
    {
    }

    public int Utilization { get; } = utilization;
    public int MaxUtilization { get; } = maxUtilization;
    public int CoreClock { get; } = coreClock;
    public int MaxCoreClock { get; } = maxCoreClock;
    public int MemoryClock { get; } = memoryClock;
    public int MaxMemoryClock { get; } = maxMemoryClock;
    // æ¸©åº¦
    public int Temperature { get; } = temperature;
    public int MaxTemperature { get; } = maxTemperature;

    // Wattage (W)
    public int Wattage { get; } = wattage;

    // Voltage (V)
    public double Voltage { get; } = voltage;

    // Min/Max values for tracking
    public double MinVoltage { get; }
    public double MaxVoltage { get; }
    public int MinTemperature { get; }
    public int MaxTemperatureRecord { get; }

    // é£æè½¬é?
    public int FanSpeed { get; } = fanSpeed;
    public int MaxFanSpeed { get; } = maxFanSpeed;

    public SensorData WithMinMax(double minVolt, double maxVolt, int minTemp, int maxTemp)
    {
        return new SensorData(
            Utilization, MaxUtilization,
            CoreClock, MaxCoreClock,
            MemoryClock, MaxMemoryClock,
            Temperature, MaxTemperature,
            Wattage, Voltage,
            FanSpeed, MaxFanSpeed,
            minVolt, maxVolt, minTemp, maxTemp);
    }

    private SensorData(
    int utilization,
    int maxUtilization,
    int coreClock,
    int maxCoreClock,
    int memoryClock,
    int maxMemoryClock,
    int temperature,
    int maxTemperature,
    int wattage,
    double voltage,
    int fanSpeed,
    int maxFanSpeed,
    double minVoltage,
    double maxVoltage,
    int minTemperature,
    int maxTemperatureRecord) : this(utilization, maxUtilization, coreClock, maxCoreClock, memoryClock, maxMemoryClock, temperature, maxTemperature, wattage, voltage, fanSpeed, maxFanSpeed)
    {
        MinVoltage = minVoltage;
        MaxVoltage = maxVoltage;
        MinTemperature = minTemperature;
        MaxTemperatureRecord = maxTemperatureRecord;
    }

    public override string ToString() =>
        $"Utilization: {Utilization}%, Clock: {CoreClock}MHz, Temp: {Temperature}C, Fan: {FanSpeed}RPM, Power: {Wattage}W, Voltage: {Voltage}V";
}

public readonly struct SensorsData(SensorData cpu, SensorData gpu)
{
    public static readonly SensorsData Empty = new(SensorData.Empty, SensorData.Empty);

    public SensorData CPU { get; } = cpu;
    public SensorData GPU { get; } = gpu;

    public override string ToString() => $"{nameof(CPU)}: {CPU}, {nameof(GPU)}: {GPU}";
}
