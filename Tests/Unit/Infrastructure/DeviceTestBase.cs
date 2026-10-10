using FluentAssertions;

namespace UniversalDeviceToolkit.Tests.Infrastructure;

/// <summary>
/// Base class for device-specific parameterized tests.
/// Provides common infrastructure and convenience methods for testing across different hardware configurations.
/// </summary>
public abstract class DeviceTestBase : UnitTestBase
{
    /// <summary>
    /// Gets all available device profiles as test data.
    /// </summary>
    public static IEnumerable<object[]> DeviceProfiles => Infrastructure.DeviceProfiles.All();

    /// <summary>
    /// Creates a Fake factory configured with the given device profile.
    /// </summary>
    protected virtual HardwareFakeFactory CreateFakeFactory(DeviceProfile profile)
    {
        return new HardwareFakeFactory(profile);
    }

    /// <summary>
    /// Gets sensor controller mock based on device profile.
    /// </summary>
    protected virtual MockISensorsController CreateSensorsMock(DeviceProfile profile)
    {
        return new MockISensorsController(profile);
    }

    protected void ShouldHaveDGPU(DeviceProfile profile)
    {
        profile.HasDgpu.Should().BeTrue();
    }

    protected void ShouldNotHaveDGPU(DeviceProfile profile)
    {
        profile.HasDgpu.Should().BeFalse();
    }

    protected void ShouldHaveBacklightType(DeviceProfile profile, KeyboardBacklightType expectedType)
    {
        profile.BacklightType.Should().Be(expectedType);
    }

    protected void ShouldHaveAtLeastNFans(DeviceProfile profile, int minCount)
    {
        profile.FanCount.Should().BeGreaterThanOrEqualTo(minCount);
    }

    protected void ShouldHaveSensorCountInRange(DeviceProfile profile, int min, int max)
    {
        profile.SensorCount.Should().BeInRange(min, max);
    }
}

/// <summary>
/// Extension helper methods for device-specific assertions.
/// </summary>
public static class DeviceProfileExtensions
{
    public static bool IsFromFamily(this DeviceProfile profile, string familyName)
    {
        return profile.DeviceFamily.Equals(familyName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool SupportsOverclocking(this DeviceProfile profile) => profile.HasOverclockSupport;

    public static int GetBatteryCapacityWh(this DeviceProfile profile) => profile.BatteryCapacityWh;

    public static IReadOnlyList<int> GetRefreshRates(this DeviceProfile profile) => profile.DisplayRefreshRates;

    public static bool HasAdvancedKeyboard(this DeviceProfile profile)
    {
        return profile.BacklightType is KeyboardBacklightType.RGB or 
               KeyboardBacklightType.Spectrum or 
               KeyboardBacklightType.Zone;
    }
}
