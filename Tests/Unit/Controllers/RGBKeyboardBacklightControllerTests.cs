using System;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class RGBKeyboardBacklightControllerTests : UnitTestBase
{
    private RGBKeyboardSettings _settings = null!;
    private VantageDisabler _vantageDisabler = null!;
    private RGBKeyboardBacklightController _controller = null!;

    protected override void Setup()
    {
        _settings = new RGBKeyboardSettings();
        _vantageDisabler = new VantageDisabler();
        _controller = new RGBKeyboardBacklightController(_settings, _vantageDisabler);
    }

    protected override void Cleanup()
    {
        _controller = null!;
        _settings = null!;
        _vantageDisabler = null!;
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _controller.Should().NotBeNull();
    }

    [Fact]
    public void ForceDisable_DefaultValue_ShouldBeFalse()
    {
        _controller.ForceDisable.Should().BeFalse();
    }

    [Fact]
    public async Task IsSupportedAsync_WhenForceDisable_ShouldReturnFalse()
    {
        _controller.ForceDisable = true;

        var result = await _controller.IsSupportedAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetStateAsync_WhenForceDisable_ShouldThrowInvalidOperationException()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.GetStateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RGB Keyboard unsupported*");
    }

    [Fact]
    public async Task SetStateAsync_WhenForceDisable_ShouldThrowInvalidOperationException()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.SetStateAsync(default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RGB Keyboard unsupported*");
    }

    [Fact]
    public async Task SetLightControlOwnerAsync_WhenForceDisable_ShouldNotThrow()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.SetLightControlOwnerAsync(true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ForceDisable_SetAndGet_ShouldReturnCorrectValue()
    {
        _controller.ForceDisable = true;
        _controller.ForceDisable.Should().BeTrue();

        _controller.ForceDisable = false;
        _controller.ForceDisable.Should().BeFalse();
    }

    #region Extended Tests

    [Fact]
    public async Task SetPresetAsync_WhenForceDisable_ShouldThrowInvalidOperationException()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.SetPresetAsync(RGBKeyboardBacklightPreset.Off);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RGB Keyboard unsupported*");
    }

    [Fact]
    public async Task SetNextPresetAsync_WhenForceDisable_ShouldThrowInvalidOperationException()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.SetNextPresetAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RGB Keyboard unsupported*");
    }

    [Fact]
    public async Task SetLightControlOwnerAsync_WithRestorePreset_WhenForceDisable_ShouldNotThrow()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.SetLightControlOwnerAsync(true, restorePreset: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetLightControlOwnerAsync_WithEnableFalse_WhenForceDisable_ShouldNotThrow()
    {
        _controller.ForceDisable = true;

        var act = async () => await _controller.SetLightControlOwnerAsync(false);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ForceDisable_AfterSettingToFalse_IsSupportedShouldCheckDevice()
    {
        _controller.ForceDisable = false;

        var result = await _controller.IsSupportedAsync();

        result.Should().BeFalse();
    }

    #endregion
}
