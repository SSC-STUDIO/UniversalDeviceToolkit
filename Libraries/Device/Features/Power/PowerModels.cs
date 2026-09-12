using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib;

public readonly struct BatteryInformation(
    bool isCharging,
    int batteryPercentage,
    int batteryLifeRemaining,
    int fullBatteryLifeRemaining,
    int dischargeRate,
    int minDischargeRate,
    int maxDischargeRate,
    int estimateChargeRemaining,
    int designCapacity,
    int fullChargeCapacity,
    int cycleCount,
    bool isLowBattery,
    double? batteryTemperatureC,
    DateTime? manufactureDate,
    DateTime? firstUseDate,
    string? modelName)
{
    public static readonly BatteryInformation Empty = new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, null, null, null, null);

    public bool IsCharging { get; } = isCharging;
    public int BatteryPercentage { get; } = batteryPercentage;
    public int BatteryLifeRemaining { get; } = batteryLifeRemaining;
    public int FullBatteryLifeRemaining { get; init; } = fullBatteryLifeRemaining;
    public int DischargeRate { get; } = dischargeRate;
    public int MinDischargeRate { get; } = minDischargeRate;
    public int MaxDischargeRate { get; } = maxDischargeRate;
    public int EstimateChargeRemaining { get; } = estimateChargeRemaining;
    public int DesignCapacity { get; } = designCapacity;
    public int FullChargeCapacity { get; } = fullChargeCapacity;
    public int CycleCount { get; } = cycleCount;
    public bool IsLowBattery { get; } = isLowBattery;
    public double? BatteryTemperatureC { get; } = batteryTemperatureC;
    public DateTime? ManufactureDate { get; } = manufactureDate;
    public DateTime? FirstUseDate { get; } = firstUseDate;
    public string? ModelName { get; } = modelName;
    public double? AvgTemperatureC { get; private init; }

    public BatteryInformation WithAvgTemp(double? avgTemperatureC) =>
        new(
            IsCharging,
            BatteryPercentage,
            BatteryLifeRemaining,
            FullBatteryLifeRemaining,
            DischargeRate,
            MinDischargeRate,
            MaxDischargeRate,
            EstimateChargeRemaining,
            DesignCapacity,
            FullChargeCapacity,
            CycleCount,
            IsLowBattery,
            BatteryTemperatureC,
            ManufactureDate,
            FirstUseDate,
            ModelName)
        {
            AvgTemperatureC = avgTemperatureC
        };

    public double BatteryHealth =>
        DesignCapacity > 0
        ? Math.Round((double)FullChargeCapacity / DesignCapacity * 100.0, 2, MidpointRounding.AwayFromZero)
        : 0.0;
}

public readonly struct WindowsPowerPlan(Guid guid, string name, bool isActive)
{
    public Guid Guid { get; } = guid;
    public string Name { get; } = name;
    public bool IsActive { get; } = isActive;

    public override string ToString() => $"{nameof(Guid)}: {Guid}, {nameof(Name)}: {Name}, {nameof(IsActive)}: {IsActive}";

    #region Equality

    public override bool Equals(object? obj) => obj is WindowsPowerPlan other && Guid.Equals(other.Guid);

    public override int GetHashCode() => Guid.GetHashCode();

    public static bool operator ==(WindowsPowerPlan left, WindowsPowerPlan right) => left.Equals(right);

    public static bool operator !=(WindowsPowerPlan left, WindowsPowerPlan right) => !left.Equals(right);

    #endregion
}
