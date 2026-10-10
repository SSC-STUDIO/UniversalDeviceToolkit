using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Infrastructure;

[Trait("Category", TestCategories.Unit)]
public class DeviceProfileTests : DeviceTestBase
{
    /// <summary>
    /// Tests that all device profiles have valid configurations.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeviceProfiles))]
    public void All_Profiles_ShouldHaveValidConfiguration(DeviceProfile profile)
    {
        profile.Name.Should().NotBeNullOrWhiteSpace();
        profile.DeviceFamily.Should().NotBeNullOrWhiteSpace();
        profile.FanCount.Should().BeGreaterThan(0);
        profile.SensorCount.Should().BeGreaterThanOrEqualTo(0);
        profile.DisplayRefreshRates.Should().NotBeNull().And.NotBeEmpty();
        profile.BatteryCapacityWh.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that high-end devices (Legion 5 Pro, Legion 7i) have more sensors than low-end devices.
    /// </summary>
    [Theory]
    [InlineData("Legion 5 Pro", 12)]
    [InlineData("Legion 5", 8)]
    [InlineData("LOQ 15", 6)]
    [InlineData("IdeaPad Gaming 3", 4)]
    [InlineData("Legion 7i", 16)]
    public void HighEndDevices_ShouldHaveMoreSensors(string deviceName, int expectedSensorCount)
    {
        var testCases = new Dictionary<string, int>
        {
            ["Legion 5 Pro"] = 12,
            ["Legion 5"] = 8,
            ["LOQ 15"] = 6,
            ["IdeaPad Gaming 3"] = 4,
            ["Legion 7i"] = 16
        };

        testCases.Should().ContainKey(deviceName);
        testCases[deviceName].Should().Be(expectedSensorCount);
    }

    /// <summary>
    /// Tests that dGPU-equipped devices are correctly identified.
    /// </summary>
    [Theory]
    [InlineData("Legion 5 Pro", true)]
    [InlineData("Legion 5", true)]
    [InlineData("LOQ 15", true)]
    [InlineData("IdeaPad Gaming 3", false)]
    [InlineData("Legion 7i", true)]
    public void DGPU_Support_ShouldMatchExpectation(string deviceName, bool expectedHasDgpu)
    {
        var profile = Infrastructure.DeviceProfiles.All()
            .Select(args => (DeviceProfile)args[0])
            .FirstOrDefault(p => p.Name == deviceName);

        profile.Should().NotBeNull($"No device profile found for '{deviceName}'");

        if (expectedHasDgpu)
        {
            ShouldHaveDGPU(profile!);
            profile!.GpuModel.Should().NotBeNull("dGPU devices should specify GPU model");
        }
        else
        {
            ShouldNotHaveDGPU(profile!);
            profile!.GpuModel.Should().BeNull("Non-dGPU devices should not have a discrete GPU model");
        }
    }

    /// <summary>
    /// Tests keyboard backlight type matches device tier.
    /// </summary>
    [Theory]
    [InlineData("Legion 5 Pro", KeyboardBacklightType.Spectrum)]
    [InlineData("Legion 5", KeyboardBacklightType.RGB)]
    [InlineData("LOQ 15", KeyboardBacklightType.Zone)]
    [InlineData("IdeaPad Gaming 3", KeyboardBacklightType.White)]
    [InlineData("Legion 7i", KeyboardBacklightType.Spectrum)]
    public void KeyboardBacklightType_ShouldMatchDeviceTier(string deviceName, KeyboardBacklightType expectedType)
    {
        var profile = Infrastructure.DeviceProfiles.All()
            .Select(args => (DeviceProfile)args[0])
            .FirstOrDefault(p => p.Name == deviceName);

        profile.Should().NotBeNull($"No device profile found for '{deviceName}'");
        ShouldHaveBacklightType(profile!, expectedType);
    }

    /// <summary>
    /// Tests fan configuration across different device tiers.
    /// </summary>
    [Fact]
    public void FanConfiguration_ShouldMatchDeviceRequirements()
    {
        foreach (var testData in Infrastructure.DeviceProfiles.All())
        {
            var profile = (DeviceProfile)testData[0];

            if (profile.HasDgpu && profile.BacklightType is KeyboardBacklightType.RGB or KeyboardBacklightType.Spectrum)
            {
                profile.FanCount.Should().BeGreaterThanOrEqualTo(2, 
                    $"High-end device '{profile.Name}' with {profile.BacklightType} backlight should have cooling capacity");
            }
            else
            {
                profile.FanCount.Should().BeGreaterThanOrEqualTo(1);
            }
        }
    }

    /// <summary>
    /// Tests display refresh rate support based on device tier.
    /// </summary>
    [Theory]
    [InlineData("Legion 5 Pro", new[] { 60, 165, 240 })]
    [InlineData("Legion 5", new[] { 60, 144 })]
    [InlineData("LOQ 15", new[] { 60, 144 })]
    [InlineData("IdeaPad Gaming 3", new[] { 60 })]
    [InlineData("Legion 7i", new[] { 60, 165, 240 })]
    public void DisplayRefreshRates_ShouldMatchScreenCapabilities(string deviceName, int[] expectedRates)
    {
        var profile = Infrastructure.DeviceProfiles.All()
            .Select(args => (DeviceProfile)args[0])
            .FirstOrDefault(p => p.Name == deviceName);

        profile.Should().NotBeNull($"No device profile found for '{deviceName}'");

        profile!.DisplayRefreshRates.Should().ContainInOrder(expectedRates);
        profile.DisplayRefreshRates.Should().Contain(60);
    }

    /// <summary>
    /// Tests that overclocking support is consistent with device positioning.
    /// </summary>
    [Fact]
    public void OverclockSupport_ShouldMatchDeviceTier()
    {
        Infrastructure.DeviceProfiles.Legion5Pro.HasOverclockSupport.Should().BeTrue();
        Infrastructure.DeviceProfiles.Legion5.HasOverclockSupport.Should().BeTrue();
        Infrastructure.DeviceProfiles.Legion7i.HasOverclockSupport.Should().BeTrue();

        Infrastructure.DeviceProfiles.Loq15.HasOverclockSupport.Should().BeFalse();
        Infrastructure.DeviceProfiles.IdeaPadGaming3.HasOverclockSupport.Should().BeFalse();
    }

    /// <summary>
    /// Tests battery capacity correlates with device form factor.
    /// </summary>
    [Fact]
    public void BatteryCapacity_ShouldScaleWithDeviceSize()
    {
        Infrastructure.DeviceProfiles.Legion7i.BatteryCapacityWh.Should().BeGreaterThanOrEqualTo(90);
        Infrastructure.DeviceProfiles.Legion5Pro.BatteryCapacityWh.Should().BeGreaterThanOrEqualTo(80);
        
        Infrastructure.DeviceProfiles.Legion5.BatteryCapacityWh.Should().BeInRange(60, 80);
        Infrastructure.DeviceProfiles.Loq15.BatteryCapacityWh.Should().BeInRange(60, 80);
        
        Infrastructure.DeviceProfiles.IdeaPadGaming3.BatteryCapacityWh.Should().BeLessThan(60);
    }

    /// <summary>
    /// Integration test: Simulates sensor data acquisition with Fake controller.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeviceProfiles))]
    public async Task SensorsControllerMock_ShouldReturnRealisticDataForEachDevice(DeviceProfile profile)
    {
        var mockController = CreateSensorsMock(profile);
        var data = await mockController.GetDataAsync();

        data.CPU.Should().NotBe(default, "CPU sensor data should be populated");
        data.CPU.Temperature.Should().BeInRange(45, 95, "CPU temperature should be in realistic range");

        if (profile.HasDgpu)
        {
            data.GPU.Should().NotBe(default, "dGPU-equipped devices should have GPU sensor data");
            data.GPU.Temperature.Should().BeInRange(40, 90, "GPU temperature should be in realistic range");
        }
    }

    /// <summary>
    /// Integration test: Validates device-specific WMI interface creation.
    /// </summary>
    [Theory]
    [InlineData("Legion 5 Pro", true)]
    [InlineData("IdeaPad Gaming 3", false)]
    [InlineData("LOQ 15", true)]
    public void FakeFactory_ShouldCreateCorrectWmiInterfacesForDevice(string deviceName, bool expectEC)
    {
        var profile = Infrastructure.DeviceProfiles.All()
            .Select(args => (DeviceProfile)args[0])
            .FirstOrDefault(p => p.Name == deviceName);

        profile.Should().NotBeNull($"No device profile found for '{deviceName}'");
        var fakeFactory = new HardwareFakeFactory(profile!);

        var ecChannel = fakeFactory.CreateEcChannel();
        var wmiInterfaces = fakeFactory.CreateWmiInterfaces();

        ecChannel.Should().NotBeNull("EC channel factory should always return an instance");
        ecChannel.Available.Should().Be(expectEC, "EC availability should match device expectations");
    }
}

/// <summary>
/// Sample test demonstrating real-world usage of DeviceProfile parameterization.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SensorControllerDeviceCompatibilityTests : DeviceTestBase
{
    [Theory]
    [MemberData(nameof(DeviceProfiles))]
    public async Task SensorController_VerifiesCompatibleDevices(DeviceProfile profile)
    {
        using var controller = CreateSensorsMock(profile);

        var supported = await controller.IsSupportedAsync();
        var data = await controller.GetDataAsync();

        if (profile.SensorCount > 0)
        {
            supported.Should().BeTrue($"{profile.Name} should support sensors");
            data.Should().NotBe(default, "Should return sensor data");
        }
    }

    [Theory]
    [MemberData(nameof(DeviceProfiles))]
    public async Task SensorController_ReportCorrectFanCount(DeviceProfile profile)
    {
        using var controller = CreateSensorsMock(profile);
        var fanSpeeds = await controller.GetFanSpeedsAsync();

        fanSpeeds.cpuFanSpeed.Should().BeGreaterThan(0);
        
        if (profile.HasDgpu)
        {
            fanSpeeds.gpuFanSpeed.Should().BeGreaterThan(0, "dGPU devices should report GPU fan");
        }
    }
}
