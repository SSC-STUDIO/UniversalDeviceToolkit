namespace UniversalDeviceToolkit.Tests.Infrastructure;

public static class DeviceProfiles
{
    public static readonly DeviceProfile Legion5Pro = new(
        Name: "Legion 5 Pro",
        DeviceFamily: "Legion",
        HasDgpu: true,
        GpuModel: "NVIDIA GeForce RTX 4060 Laptop",
        FanCount: 3,
        SensorCount: 12,
        BacklightType: KeyboardBacklightType.Spectrum,
        DisplayRefreshRates: [60, 165, 240],
        HasOverclockSupport: true,
        BatteryCapacityWh: 80);

    public static readonly DeviceProfile Legion5 = new(
        Name: "Legion 5",
        DeviceFamily: "Legion",
        HasDgpu: true,
        GpuModel: "NVIDIA GeForce RTX 3060 Laptop",
        FanCount: 2,
        SensorCount: 8,
        BacklightType: KeyboardBacklightType.RGB,
        DisplayRefreshRates: [60, 144],
        HasOverclockSupport: true,
        BatteryCapacityWh: 60);

    public static readonly DeviceProfile Loq15 = new(
        Name: "LOQ 15",
        DeviceFamily: "LOQ",
        HasDgpu: true,
        GpuModel: "NVIDIA GeForce RTX 4050 Laptop",
        FanCount: 2,
        SensorCount: 6,
        BacklightType: KeyboardBacklightType.Zone,
        DisplayRefreshRates: [60, 144],
        HasOverclockSupport: false,
        BatteryCapacityWh: 60);

    public static readonly DeviceProfile IdeaPadGaming3 = new(
        Name: "IdeaPad Gaming 3",
        DeviceFamily: "IdeaPad",
        HasDgpu: false,
        GpuModel: null,
        FanCount: 1,
        SensorCount: 4,
        BacklightType: KeyboardBacklightType.White,
        DisplayRefreshRates: [60],
        HasOverclockSupport: false,
        BatteryCapacityWh: 45);

    public static readonly DeviceProfile Legion7i = new(
        Name: "Legion 7i",
        DeviceFamily: "Legion",
        HasDgpu: true,
        GpuModel: "NVIDIA GeForce RTX 4090 Laptop",
        FanCount: 3,
        SensorCount: 16,
        BacklightType: KeyboardBacklightType.Spectrum,
        DisplayRefreshRates: [60, 165, 240],
        HasOverclockSupport: true,
        BatteryCapacityWh: 99);

    public static IEnumerable<object[]> All() => new object[][]
    {
        [Legion5Pro],
        [Legion5],
        [Loq15],
        [IdeaPadGaming3],
        [Legion7i],
    };
}
