using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

public class ThrottleFirstDispatcherEdgeCaseTests
{
    [Fact]
    public void Constructor_NegativeInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThrottleFirstDispatcher(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_ZeroInterval_IsAllowed()
    {
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.Zero);
        dispatcher.Interval.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task DispatchAsync_NullTask_Throws()
    {
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(null!));
    }

    [Fact]
    public async Task DispatchAsync_FirstCall_ExecuteImmediately()
    {
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromMinutes(1));
        var executed = false;

        await dispatcher.DispatchAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_SecondCallWithinInterval_IsThrottled()
    {
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromHours(1));
        var callCount = 0;

        await dispatcher.DispatchAsync(() =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        await dispatcher.DispatchAsync(() =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ResetAsync_AllowsNextDispatch()
    {
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromHours(1));
        var callCount = 0;

        await dispatcher.DispatchAsync(() => { callCount++; return Task.CompletedTask; });
        await dispatcher.ResetAsync();
        await dispatcher.DispatchAsync(() => { callCount++; return Task.CompletedTask; });

        callCount.Should().Be(2);
    }
}

public class ThrottleLastDispatcherEdgeCaseTests
{
    [Fact]
    public void Constructor_NegativeInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThrottleLastDispatcher(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_ZeroInterval_IsAllowed()
    {
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.Zero);
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public async Task DispatchAsync_NullTask_Throws()
    {
        using var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(null!));
    }

    [Fact]
    public async Task DispatchAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMinutes(1));
        dispatcher.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => dispatcher.DispatchAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task DispatchAsync_ExecuteTask()
    {
        using var dispatcher = new ThrottleLastDispatcher(TimeSpan.Zero);
        var executed = false;

        await dispatcher.DispatchAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMinutes(1));
        dispatcher.Dispose();
        dispatcher.Dispose();
    }
}
