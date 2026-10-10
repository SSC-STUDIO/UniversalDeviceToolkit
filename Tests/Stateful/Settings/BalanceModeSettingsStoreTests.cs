using FluentAssertions;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class BalanceModeSettingsStoreTests
{
    [Fact]
    public void BalanceModeSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new BalanceModeSettings.BalanceModeSettingsStore();
        store.AIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void BalanceModeSettingsStore_SetValues_ShouldWork()
    {
        var store = new BalanceModeSettings.BalanceModeSettingsStore
        {
            AIModeEnabled = true
        };
        store.AIModeEnabled.Should().BeTrue();
    }
}
