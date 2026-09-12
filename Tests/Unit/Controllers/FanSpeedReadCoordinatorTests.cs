using FluentAssertions;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

public class FanSpeedReadCoordinatorTests
{
    [Fact]
    public async Task ReadAsync_WhenFirstSourceReturnsPositive_ReturnsImmediately()
    {
        var result = await FanSpeedReadCoordinator.ReadAsync(
            "CPU",
            new FanSpeedSourceReader(FanSpeedSource.LenovoFanMethod, () => Task.FromResult((true, 2400))),
            new FanSpeedSourceReader(FanSpeedSource.LenovoGamezone, () => Task.FromResult((true, 1800))));

        result.Rpm.Should().Be(2400);
        result.Source.Should().Be(FanSpeedSource.LenovoFanMethod);
        result.IsExplicitlyStopped.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenSourceReturnsExplicitZero_TreatsAsParked()
    {
        var result = await FanSpeedReadCoordinator.ReadAsync(
            "GPU",
            new FanSpeedSourceReader(FanSpeedSource.LenovoFanMethod, () => Task.FromResult((false, -1))),
            new FanSpeedSourceReader(FanSpeedSource.LenovoGamezone, () => Task.FromResult((true, 0))));

        result.Rpm.Should().Be(0);
        result.Source.Should().Be(FanSpeedSource.LenovoGamezone);
        result.IsExplicitlyStopped.Should().BeTrue();
    }

    [Fact]
    public async Task ReadAsync_WhenAllSourcesUnavailable_ReturnsUnavailable()
    {
        var result = await FanSpeedReadCoordinator.ReadAsync(
            "CPU",
            new FanSpeedSourceReader(FanSpeedSource.LenovoCapability, () => Task.FromResult((false, -1))),
            new FanSpeedSourceReader(FanSpeedSource.LenovoGamezone, () => Task.FromResult((false, -1))),
            new FanSpeedSourceReader(FanSpeedSource.LenovoFanMethod, () => Task.FromResult((false, -1))));

        result.Rpm.Should().Be(-1);
        result.Source.Should().Be(FanSpeedSource.Unavailable);
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_WhenLaterSourceSucceeds_UsesFallbackOrder()
    {
        var result = await FanSpeedReadCoordinator.ReadAsync(
            "CPU",
            new FanSpeedSourceReader(FanSpeedSource.LenovoCapability, () => Task.FromResult((false, -1))),
            new FanSpeedSourceReader(FanSpeedSource.LenovoGamezone, () => Task.FromResult((true, 3100))));

        result.Rpm.Should().Be(3100);
        result.Source.Should().Be(FanSpeedSource.LenovoGamezone);
    }

    [Fact]
    public async Task ReadAsync_WhenSourceThrows_ContinuesToNextSource()
    {
        var result = await FanSpeedReadCoordinator.ReadAsync(
            "CPU",
            new FanSpeedSourceReader(FanSpeedSource.LenovoCapability, () => throw new InvalidOperationException("probe failed")),
            new FanSpeedSourceReader(FanSpeedSource.LenovoGamezone, () => Task.FromResult((true, 2750))));

        result.Rpm.Should().Be(2750);
        result.Source.Should().Be(FanSpeedSource.LenovoGamezone);
    }

    [Fact]
    public async Task ReadAsync_WhenExplicitZeroReturned_DoesNotInvokeLaterSources()
    {
        var laterInvoked = false;
        var result = await FanSpeedReadCoordinator.ReadAsync(
            "GPU",
            new FanSpeedSourceReader(FanSpeedSource.LenovoGamezone, () => Task.FromResult((true, 0))),
            new FanSpeedSourceReader(FanSpeedSource.LenovoFanMethod, () =>
            {
                laterInvoked = true;
                return Task.FromResult((true, 3200));
            }));

        result.Rpm.Should().Be(0);
        laterInvoked.Should().BeFalse();
    }
}