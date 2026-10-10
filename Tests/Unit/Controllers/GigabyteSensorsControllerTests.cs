using System.Reflection;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public class GigabyteSensorsControllerTests
{
    private sealed class FakeGigabyteWmi : IGigabyteWmi
    {
        private readonly Dictionary<string, int> _values = new();
        public bool Available { get; set; } = true;

        public bool IsAvailable => Available;

        public void Seed(string methodName, int value) => _values[methodName] = value;

        public int GetValue(string methodName) =>
            _values.TryGetValue(methodName, out var value) ? value : -1;
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_WhenMofClassesMissing()
    {
        SetMachineInformation(GigabyteMachine());
        var controller = new GigabyteSensorsController(null!, new FakeGigabyteWmi { Available = false });

        (await controller.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public async Task IsSupported_ShouldBeFalse_OnNonGigabyteMachine()
    {
        SetMachineInformation(new MachineInformation { Vendor = "LENOVO", MachineType = "83DF", Model = "Legion Y9000P IRX9" });
        var wmi = new FakeGigabyteWmi();
        wmi.Seed("getCpuTemp", 55);
        var controller = new GigabyteSensorsController(null!, wmi);

        (await controller.IsSupportedAsync()).Should().BeFalse();
        ResetCompatibilityCache();
    }

    [Fact]
    public void Reads_ShouldUseVendorValues_WhenPresent()
    {
        var wmi = new FakeGigabyteWmi();
        wmi.Seed("getCpuTemp", 62);
        wmi.Seed("getGpuTemp1", 58);
        wmi.Seed("getRpm1", 3200);
        wmi.Seed("getRpm2", 3400);

        wmi.GetValue("getCpuTemp").Should().Be(62);
        wmi.GetValue("getGpuTemp1").Should().Be(58);
        wmi.GetValue("getRpm1").Should().Be(3200);
        wmi.GetValue("getRpm2").Should().Be(3400);
        wmi.GetValue("getRpm3").Should().Be(-1);
    }

    private static MachineInformation GigabyteMachine() => new()
    {
        Vendor = "Gigabyte Technology Co., Ltd.",
        MachineType = "0000",
        Model = "AORUS 16X"
    };

    private static void SetMachineInformation(MachineInformation machineInformation)
    {
        var lazy = new Lazy<Task<MachineInformation>>(() => Task.FromResult(machineInformation));
        typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, lazy);
    }

    private static void ResetCompatibilityCache()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        var lazyField = typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static);
        if (lazyField != null)
        {
            var method = typeof(Compatibility).GetMethod("GetMachineInformationInternalAsync", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                var del = Delegate.CreateDelegate(typeof(Func<Task<MachineInformation>>), method);
                var newLazy = Activator.CreateInstance(typeof(Lazy<Task<MachineInformation>>), [del, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication]);
                lazyField.SetValue(null, newLazy);
            }
        }
        typeof(Compatibility).GetField("_isCompatible", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    }
}
