using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Lighting;
using UniversalDeviceToolkit.Lib.Listeners;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public sealed class SpecialKeyAndLightingTests
{
    [Fact]
    public void SpecialKeyDiscovery_Catalog_CoversEnumMembersUsedInListener()
    {
        var ids = SpecialKeyDiscovery.All.Select(d => d.Id).ToHashSet();
        ids.Should().Contain(SpecialKey.FnF9);
        ids.Should().Contain(SpecialKey.SpectrumPreset1);
        ids.Should().Contain(SpecialKey.WhiteBacklight1);
        SpecialKeyDiscovery.All.Should().OnlyContain(d => d.SupportsSingleClick);
    }

    [Fact]
    public void SpecialKeyDiscovery_NonLegion_HidesAllKeys()
    {
        var filtered = SpecialKeyDiscovery.FilterForDevice(
            isLegionMachine: false,
            spectrumSupported: true,
            whiteKeyboardSupported: true);

        filtered.Should().BeEmpty("non-Legion machines must not show Lenovo special-key UI");
    }

    [Fact]
    public void SpecialKeyDiscovery_SpectrumOff_HidesSpectrumKeysOnly()
    {
        var filtered = SpecialKeyDiscovery.FilterForDevice(
            isLegionMachine: true,
            spectrumSupported: false,
            whiteKeyboardSupported: true);

        filtered.Should().NotBeEmpty();
        filtered.Should().NotContain(d => d.RequiresSpectrumDevice);
        filtered.Should().Contain(d => d.Id == SpecialKey.FnF9);
        filtered.Should().Contain(d => d.RequiresWhiteKeyboardDevice);
    }

    [Fact]
    public void SpecialKeyDiscovery_WhiteOff_HidesWhiteKeysOnly()
    {
        var filtered = SpecialKeyDiscovery.FilterForDevice(
            isLegionMachine: true,
            spectrumSupported: true,
            whiteKeyboardSupported: false);

        filtered.Should().NotContain(d => d.RequiresWhiteKeyboardDevice);
        filtered.Should().Contain(d => d.RequiresSpectrumDevice);
    }

    [Fact]
    public void SpecialKeyLedIsolation_Failure_DoesNotThrow_AndInvokesCallback()
    {
        Exception? observed = null;
        var actionRanPast = false;

        SpecialKeyLedIsolation.RunLedFeedback(
            "test-led",
            () => throw new InvalidOperationException("led-broken"),
            ex => observed = ex);

        actionRanPast = true;
        actionRanPast.Should().BeTrue();
        observed.Should().BeOfType<InvalidOperationException>();
        observed!.Message.Should().Be("led-broken");
    }

    [Fact]
    public async Task SpecialKeyLedIsolation_AsyncFailure_DoesNotThrow()
    {
        var failed = false;
        await SpecialKeyLedIsolation.RunLedFeedbackAsync(
            "test-led-async",
            () => throw new InvalidOperationException("async-led"),
            _ => failed = true);

        failed.Should().BeTrue();
    }

    [Fact]
    public async Task Keyboard24Zone_IsSupported_AlwaysFalseUntilVerified()
    {
        (await Keyboard24ZoneLightingCapability.IsSupportedAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task LightingCapabilityGate_Regions_IncludeGatedAmbientAnd24Zone()
    {
        // Without IoC spectrum controller, spectrum region is unsupported — still must list regions.
        var regions = await LightingCapabilityGate.GetRegionsAsync();
        regions.Should().Contain(r => r.RegionId == LightingCapabilityGate.RegionSpectrum24Zone && !r.Supported);
        regions.Should().Contain(r => r.RegionId == LightingCapabilityGate.RegionFrontAmbient && !r.Supported);
        regions.Should().Contain(r => r.RegionId == LightingCapabilityGate.RegionRearAmbient && !r.Supported);
        regions.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Protocol));
    }

    [Fact]
    public async Task LightingCapabilityGate_UnknownRegion_IsUnsupported()
    {
        (await LightingCapabilityGate.IsRegionSupportedAsync("not-a-real-region")).Should().BeFalse();
    }
}
