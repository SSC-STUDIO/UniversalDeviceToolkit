using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Utils;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features.Hybrid;

[Trait("Category", TestCategories.Unit)]
public class AbstractDGPUNotifyTests
{
    [Fact]
    public void IsHardwareIdMissing_WhenEmptyOrDefault_ShouldBeTrue()
    {
        AbstractDGPUNotify.IsHardwareIdMissing(HardwareId.Empty).Should().BeTrue();
        AbstractDGPUNotify.IsHardwareIdMissing(default).Should().BeTrue();
        AbstractDGPUNotify.IsHardwareIdMissing(new HardwareId("", "2684")).Should().BeTrue();
        AbstractDGPUNotify.IsHardwareIdMissing(new HardwareId("10DE", "")).Should().BeTrue();
    }

    [Fact]
    public void HardwareIdsEqual_WhenHexPaddingOrCaseDiffers_ShouldMatch()
    {
        AbstractDGPUNotify.HardwareIdsEqual(new HardwareId("10DE", "2684"), new HardwareId("10de", "2684")).Should().BeTrue();
        AbstractDGPUNotify.HardwareIdsEqual(new HardwareId("DE", "1A"), new HardwareId("00DE", "001A")).Should().BeTrue();
        AbstractDGPUNotify.HardwareIdsEqual(new HardwareId("10DE", "2684"), new HardwareId("1002", "2684")).Should().BeFalse();
        AbstractDGPUNotify.HardwareIdsEqual(HardwareId.Empty, HardwareId.Empty).Should().BeFalse();
    }

    [Fact]
    public async Task IsDGPUAvailableAsync_WhenHardwareIdIsMissing_ShouldReturnFalseWithoutThrowing()
    {
        var notify = new TestDGPUNotify(new DefaultDelayProvider())
        {
            HardwareId = HardwareId.Empty,
        };

        var available = await notify.IsDGPUAvailableAsync();

        available.Should().BeFalse();
        notify.NotifyCallCount.Should().Be(0);
    }

    [Fact]
    public async Task NotifyAsync_WhenHardwareIdIsMissing_ShouldNotNotifyFirmware()
    {
        var notify = new TestDGPUNotify(new DefaultDelayProvider())
        {
            HardwareId = HardwareId.Empty,
        };

        await notify.NotifyAsync();

        notify.NotifyCallCount.Should().Be(0);
        notify.LastNotifiedState.Should().BeNull();
    }

    private sealed class TestDGPUNotify(IDelayProvider delayProvider) : AbstractDGPUNotify(delayProvider)
    {
        public HardwareId HardwareId { get; init; } = HardwareId.Empty;
        public int NotifyCallCount { get; private set; }
        public bool? LastNotifiedState { get; private set; }

        public override Task<bool> IsSupportedAsync() => Task.FromResult(true);

        protected override Task NotifyDGPUStatusAsync(bool state)
        {
            NotifyCallCount++;
            LastNotifiedState = state;
            return Task.CompletedTask;
        }

        protected override Task<HardwareId> GetDGPUHardwareIdAsync() => Task.FromResult(HardwareId);
    }
}
