using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.System.Razer;

namespace UniversalDeviceToolkit.Tests.Infrastructure;

/// <summary>
/// Fake implementation of EC channel for testing.
/// Mirrors the pattern used in MsiPowerModeFeatureTests.
/// </summary>
public sealed class FakeEcChannel : IEcChannel
{
    private readonly Dictionary<byte, byte> _ram = new();
    public bool Available { get; set; } = true;
    public List<(byte Address, byte Value)> Writes { get; } = [];
    public bool WriteSucceeds { get; set; } = true;

    public bool IsAvailable => Available;

    public void Seed(byte address, byte value) => _ram[address] = value;

    public bool TryRead(byte address, out byte value)
    {
        if (_ram.TryGetValue(address, out var stored))
        {
            value = stored;
            return true;
        }

        value = 0;
        return false;
    }

    public bool TryWrite(byte address, byte value)
    {
        Writes.Add((address, value));
        if (!WriteSucceeds)
            return false;

        _ram[address] = value;
        return true;
    }
}

/// <summary>
/// Fake implementation of Acer WMI interface.
/// </summary>
public sealed class FakeAcerWmi : IAcerWmi
{
    public bool IsAvailable { get; set; } = true;

    public (bool Ok, long Output) Execute(string methodName, uint input)
    {
        if (!IsAvailable)
            return (false, -1);

        return (true, 0);
    }
}

/// <summary>
/// Fake implementation of Alienware WMI interface.
/// </summary>
public sealed class FakeAlienwareWmi : IAlienwareWmi
{
    public bool IsAvailable { get; set; } = true;

    public int Execute(string methodName, byte operation, byte arg1 = 0, byte arg2 = 0, byte arg3 = 0)
    {
        if (!IsAvailable)
            return -1;

        return 0;
    }
}

/// <summary>
/// Fake implementation of HP WMI Bios interface.
/// </summary>
public sealed class FakeHpWmiBios : IHpWmiBios
{
    public bool IsAvailable { get; set; } = true;

    public (int ReturnCode, byte[] Data) Execute(uint commandType, byte[] input)
    {
        if (!IsAvailable)
            return (-1, []);

        return (0, [1]);
    }
}

/// <summary>
/// Fake implementation of Asus ATK Driver interface.
/// </summary>
public sealed class FakeAsusAtkDriver : IAsusAtkDriver
{
    public bool IsAvailable { get; set; } = true;

    public int DeviceGet(uint deviceId) => IsAvailable ? 0 : -1;

    public int DeviceSet(uint deviceId, int value) => IsAvailable ? 1 : -1;
}

/// <summary>
/// Fake implementation of Gigabyte WMI interface.
/// </summary>
public sealed class FakeGigabyteWmi : IGigabyteWmi
{
    public bool IsAvailable { get; set; } = true;

    public int GetValue(string methodName) => IsAvailable ? 0 : -1;
}

/// <summary>
/// Fake implementation of Razer HID interface.
/// </summary>
public sealed class FakeRazerHid : IRazerHid
{
    public string[] Paths { get; set; } = [];
    public bool SendSucceeds { get; set; } = true;
    public bool GetSucceeds { get; set; } = true;

    public string[] EnumerateDevicePaths(ushort vendorId) => Paths;

    public bool GetVidPid(string devicePath, out ushort vendorId, out ushort productId)
    {
        vendorId = 0x1532;
        productId = 0x0001;
        return true;
    }

    public bool TrySendFeatureReport(string devicePath, byte[] report) => SendSucceeds;

    public bool TryGetFeatureReport(string devicePath, byte[] report) => GetSucceeds;
}

/// <summary>
/// Fake implementation of Razer HID Controller.
/// </summary>
public sealed class FakeRazerHidController : IRazerHidController
{
    public bool ProbeResult { get; set; } = true;
    public Dictionary<int, int> PerformanceModes { get; set; } = new();
    public bool WriteSucceeds { get; set; } = true;

    public bool Probe() => ProbeResult;

    public int? GetPerformanceMode(byte zone)
    {
        return PerformanceModes.TryGetValue(zone, out var mode) ? mode : null;
    }

    public bool SetPerformanceMode(byte zone, byte mode, bool manualFan)
    {
        if (WriteSucceeds)
        {
            PerformanceModes[zone] = mode;
            return true;
        }
        return false;
    }

    public int? GetFanRpm(byte zone) => null;
}

/// <summary>
/// Factory for generating device-specific Fake implementations.
/// Maps DeviceProfile hardware characteristics to appropriate Fake configurations.
/// </summary>
public class HardwareFakeFactory
{
    private readonly DeviceProfile _profile;

    public HardwareFakeFactory(DeviceProfile profile)
    {
        _profile = profile;
    }

    /// <summary>
    /// Creates FakeECChannel with configuration based on device profile.
    /// MSI and Lenovo devices typically have EC support.
    /// </summary>
    public FakeEcChannel CreateEcChannel()
    {
        return new FakeEcChannel 
        { 
            Available = _profile.HasDgpu || _profile.DeviceFamily == "Legion",
            WriteSucceeds = true
        };
    }

    /// <summary>
    /// Creates manufacturer-specific WMI interfaces based on device family.
    /// </summary>
    public IEnumerable<KeyValuePair<Type, object>> CreateWmiInterfaces()
    {
        var interfaces = new List<KeyValuePair<Type, object>>();

        switch (_profile.DeviceFamily)
        {
            case "Legion" when _profile.Name.Contains("Y9000"):
                // Legion Y9000 series uses Lenovo-specific WMI (handled by base system)
                break;
                
            case "ROG" or "Republic of Gamers":
                interfaces.Add(new KeyValuePair<Type, object>(
                    typeof(IAsusAtkDriver),
                    new FakeAsusAtkDriver { IsAvailable = true }));
                break;

            case "Omen" or "Victus":
                interfaces.Add(new KeyValuePair<Type, object>(
                    typeof(IHpWmiBios),
                    new FakeHpWmiBios { IsAvailable = true }));
                break;

            case "Alienware":
                interfaces.Add(new KeyValuePair<Type, object>(
                    typeof(IAlienwareWmi),
                    new FakeAlienwareWmi { IsAvailable = true }));
                break;

            case "Predator" or "Nitro":
                interfaces.Add(new KeyValuePair<Type, object>(
                    typeof(IAcerWmi),
                    new FakeAcerWmi { IsAvailable = true }));
                break;

            case "Aorus" or "Gaming":
                interfaces.Add(new KeyValuePair<Type, object>(
                    typeof(IGigabyteWmi),
                    new FakeGigabyteWmi { IsAvailable = true }));
                break;

            case "MSI":
                break;
        }

        return interfaces;
    }

    /// <summary>
    /// Creates all necessary Fakes for a complete testing setup.
    /// </summary>
    public (FakeEcChannel EcChannel, IDictionary<Type, object> WmiInterfaces) CreateCompleteSetup()
    {
        var ecChannel = CreateEcChannel();
        var wmiInterfaces = CreateWmiInterfaces().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return (ecChannel, wmiInterfaces);
    }
}

/// <summary>
/// Mock implementation of ISensorsController for testing.
/// Simulates sensor data based on DeviceProfile configuration.
/// </summary>
public class MockISensorsController : ISensorsController
{
    private readonly DeviceProfile _profile;
    private bool _disposed;
    private readonly Random _random = new(42); // Fixed seed for reproducibility

    public MockISensorsController(DeviceProfile profile)
    {
        _profile = profile;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(_profile.SensorCount > 0);

    public Task PrepareAsync() => Task.CompletedTask;

    public Task<SensorsData> GetDataAsync(bool detailed = false)
    {
        // Build a real SensorsData object using the actual type from Lib
        var cpu = new SensorData(
            utilization: 30 + _random.Next(40),
            maxUtilization: 100,
            coreClock: 2000 + _random.Next(2000),
            maxCoreClock: 4800,
            memoryClock: 0,
            maxMemoryClock: 0,
            temperature: 45 + _random.Next(20),
            maxTemperature: 95,
            fanSpeed: _random.Next(800, 4000),
            maxFanSpeed: 5000);

        var gpu = _profile.HasDgpu
            ? new SensorData(
                utilization: 20 + _random.Next(50),
                maxUtilization: 100,
                coreClock: 1000 + _random.Next(1500),
                maxCoreClock: 2500,
                memoryClock: 6000,
                maxMemoryClock: 8000,
                temperature: 40 + _random.Next(30),
                maxTemperature: 90,
                fanSpeed: _profile.FanCount >= 2 ? _random.Next(800, 4000) : 0,
                maxFanSpeed: 5000)
            : SensorData.Empty;

        var data = new SensorsData(cpu, gpu);
        return Task.FromResult(data);
    }

    public Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync()
    {
        int cpuFan = _random.Next(800, 4000);
        int gpuFan = _profile.FanCount >= 2 ? _random.Next(800, 4000) : 0;
        return Task.FromResult((cpuFan, gpuFan));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
