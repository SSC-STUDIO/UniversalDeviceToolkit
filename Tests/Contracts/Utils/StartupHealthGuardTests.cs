using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Guard)]
public class StartupHealthGuardTests : IDisposable
{
    private readonly string _tempAppDataRoot;

    public StartupHealthGuardTests()
    {
        _tempAppDataRoot = Path.Combine(Path.GetTempPath(), "udt_safestart_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempAppDataRoot);
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _tempAppDataRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, null);
        try
        {
            if (Directory.Exists(_tempAppDataRoot))
                Directory.Delete(_tempAppDataRoot, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void TryRunStep_SuccessfulStep_DoesNotIncrementFailureCount()
    {
        var guard = new StartupHealthGuard();
        guard.RegisterStep("boot", TimeSpan.FromSeconds(5));

        var ok = guard.TryRunStep("boot", () => { }, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        guard.ConsecutiveFailureCount.Should().Be(0);
        guard.ShouldEnterSafeMode.Should().BeFalse();
    }

    [Fact]
    public void TryRunStep_ThrowingStep_ReturnsFalseAndIncrementsFailureCount()
    {
        var guard = new StartupHealthGuard();
        guard.RegisterStep("boot", TimeSpan.FromSeconds(5));

        var ok = guard.TryRunStep("boot", () => throw new InvalidOperationException("boom"), out var error);

        ok.Should().BeFalse();
        error.Should().BeOfType<InvalidOperationException>();
        error!.Message.Should().Be("boom");
        guard.ConsecutiveFailureCount.Should().Be(1);
        guard.ShouldEnterSafeMode.Should().BeFalse();
    }

    [Fact]
    public void TryRunStep_TimeoutStep_ReturnsFalseWithTimeoutException()
    {
        var guard = new StartupHealthGuard();
        // Keep a wide margin vs. OS timer resolution on hosted CI runners.
        guard.RegisterStep("slow", TimeSpan.FromMilliseconds(50));

        var ok = guard.TryRunStep(
            "slow",
            () =>
            {
                var deadline = Environment.TickCount64 + 400;
                while (Environment.TickCount64 < deadline)
                    Thread.SpinWait(64);
            },
            out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().BeAssignableTo<TimeoutException>();
        guard.ConsecutiveFailureCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConsecutiveFailureCount_TripsSafeMode_AfterThreshold()
    {
        var guard = new StartupHealthGuard(consecutiveFailureThreshold: 3);
        guard.RegisterStep("boot", TimeSpan.FromSeconds(5));

        var ok1 = guard.TryRunStep("boot", () => throw new InvalidOperationException("1"), out _);
        var ok2 = guard.TryRunStep("boot", () => throw new InvalidOperationException("2"), out _);
        var ok3 = guard.TryRunStep("boot", () => throw new InvalidOperationException("3"), out _);

        ok1.Should().BeFalse();
        ok2.Should().BeFalse();
        ok3.Should().BeFalse();
        guard.ConsecutiveFailureCount.Should().Be(3);
        guard.ShouldEnterSafeMode.Should().BeTrue();

        var changed = new List<int>();
        guard.ConsecutiveFailuresChanged += (_, count) => changed.Add(count);
        guard.ResetFailureCount();
        changed.Should().Contain(0);
    }

    [Fact]
    public void ResetFailureCount_AfterTripsSafeMode_ClearsLatchAndCounter()
    {
        var guard = new StartupHealthGuard(consecutiveFailureThreshold: 2);
        guard.RegisterStep("boot", TimeSpan.FromSeconds(5));

        guard.TryRunStep("boot", () => throw new InvalidOperationException("a"), out _);
        guard.TryRunStep("boot", () => throw new InvalidOperationException("b"), out _);

        guard.ShouldEnterSafeMode.Should().BeTrue();

        guard.ResetFailureCount();

        guard.ConsecutiveFailureCount.Should().Be(0);
        guard.ShouldEnterSafeMode.Should().BeFalse();
    }

    [Fact]
    public void TryRunStep_EmptyStepName_FailsAndDoesNotThrow()
    {
        var guard = new StartupHealthGuard();

        var ok = guard.TryRunStep(string.Empty, () => { }, out var error);

        ok.Should().BeFalse();
        error.Should().BeOfType<ArgumentException>();
        guard.ConsecutiveFailureCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TryRunStepAsync_AsyncThrowingStep_RecordsFailure()
    {
        var guard = new StartupHealthGuard();
        guard.RegisterStep("async", TimeSpan.FromSeconds(5));

        var (ok, error) = await guard.TryRunStepAsync("async", () => throw new InvalidOperationException("async-boom"));

        ok.Should().BeFalse();
        error.Should().BeOfType<InvalidOperationException>();
        guard.ConsecutiveFailureCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentFailureCount_NoExceptionUnderLoad()
    {
        var guard = new StartupHealthGuard(consecutiveFailureThreshold: 1000);
        guard.RegisterStep("ct", TimeSpan.FromSeconds(1));

        var tasks = new List<Task>();
        var exceptions = new List<Exception>();
        var lockObj = new object();

        for (var i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    guard.TryRunStep("ct", () => throw new InvalidOperationException("x"), out _);
                }
                catch (Exception ex)
                {
                    lock (lockObj) exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        exceptions.Should().BeEmpty();
        guard.ConsecutiveFailureCount.Should().Be(50);
    }

    [Fact]
    public void MarkShouldEnterSafeMode_EngagesLatchAndLogsViaEvent()
    {
        var guard = new StartupHealthGuard();
        var previousLatched = false;
        var newCount = -1;
        guard.ConsecutiveFailuresChanged += (_, count) => newCount = count;

        guard.MarkShouldEnterSafeMode();

        guard.ShouldEnterSafeMode.Should().BeTrue();
        previousLatched.Should().BeFalse();

        newCount.Should().Be(-1, "MarkShouldEnterSafeMode should not raise ConsecutiveFailuresChanged");
    }

    [Fact]
    public void ReadPersistedConsecutiveFailureCount_WhenNoMarker_ReturnsZero()
    {
        StartupHealthGuard.WritePersistedState(0, false);

        StartupHealthGuard.ReadPersistedConsecutiveFailureCount().Should().Be(0);
    }

    [Fact]
    public void WritePersistedState_ThenRead_ReturnsSameValue()
    {
        StartupHealthGuard.WritePersistedState(7, true);
        try
        {
            StartupHealthGuard.ReadPersistedConsecutiveFailureCount().Should().Be(7);
        }
        finally
        {
            StartupHealthGuard.WritePersistedState(0, false);
        }
    }

    [Fact]
    public void WritePersistedState_ZeroCount_DeletesMarkerFile()
    {
        StartupHealthGuard.WritePersistedState(2, false);
        StartupHealthGuard.WritePersistedState(0, false);

        var path = Path.Combine(Folders.AppData, "startup_health.json");
        File.Exists(path).Should().BeFalse();
    }
}
