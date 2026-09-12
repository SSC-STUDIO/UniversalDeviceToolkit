using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class PerformanceEffectVerifierTests
{
    [Fact]
    public void Verify_WhenControlSucceeds_ShouldSampleTelemetryBeforeAndAfter()
    {
        var events = new List<string>();
        var sampleCount = 0;
        var options = CreateOptions();
        var verifier = new PerformanceEffectVerifier(
            () =>
            {
                events.Add($"sample-{sampleCount}");
                return sampleCount++ == 0
                    ? CpuFrequencySummary.From([
                        new CpuFrequencyReading("cpu0", 1000, "test"),
                        new CpuFrequencyReading("cpu1", 1200, "test")
                    ])
                    : CpuFrequencySummary.From([
                        new CpuFrequencyReading("cpu0", 2200, "test"),
                        new CpuFrequencyReading("cpu1", 2400, "test")
                    ]);
            },
            (controlId, value) =>
            {
                events.Add($"set-{controlId}-{value}");
                return new HardwareControlSetResult(true, controlId, value, "changed");
            },
            () => events.Add("wait"),
            options);

        var report = verifier.Verify("cpu-governor", "performance");

        events.Should().Equal("sample-0", "set-cpu-governor-performance", "wait", "sample-1");
        report.Succeeded.Should().BeTrue();
        report.ControlId.Should().Be("cpu-governor");
        report.RequestedValue.Should().Be("performance");
        report.BeforeCpuFrequency.Should().Be(new CpuFrequencySummary(2, 1100, 1000, 1200, "test"));
        report.AfterCpuFrequency.Should().Be(new CpuFrequencySummary(2, 2300, 2200, 2400, "test"));
        report.AverageFrequencyDeltaMHz.Should().Be(1200);
        report.SampleDuration.Should().Be(options.LoadSampleDuration);
        report.LoadWorkerCount.Should().Be(options.LoadWorkerCount);
        report.Notes.Should().BeEmpty();
    }

    [Fact]
    public void Verify_WhenControlFails_ShouldStillSampleAfterAndReportNotes()
    {
        var sampleCount = 0;
        var verifier = new PerformanceEffectVerifier(
            () =>
            {
                sampleCount++;
                return CpuFrequencySummary.From([]);
            },
            (_, _) => new HardwareControlSetResult(false, "vendor-hardware-controls", "performance", "not writable"),
            () => { },
            CreateOptions());

        var report = verifier.Verify("vendor-hardware-controls", "performance");

        sampleCount.Should().Be(2);
        report.Succeeded.Should().BeFalse();
        report.ControlResult.Detail.Should().Be("not writable");
        report.BeforeCpuFrequency.Count.Should().Be(0);
        report.AfterCpuFrequency.Count.Should().Be(0);
        report.AverageFrequencyDeltaMHz.Should().BeNull();
        report.Notes.Should().Contain("Control write failed: not writable");
        report.Notes.Should().Contain(note => note.Contains("before", StringComparison.OrdinalIgnoreCase));
        report.Notes.Should().Contain(note => note.Contains("after", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Verify_WhenAverageFrequencyIsUnchanged_ShouldReturnEvidenceNote()
    {
        var verifier = new PerformanceEffectVerifier(
            () => CpuFrequencySummary.From([new CpuFrequencyReading("cpu0", 1800, "test")]),
            (controlId, value) => new HardwareControlSetResult(true, controlId, value, "changed"),
            () => { },
            CreateOptions());

        var report = verifier.Verify("power-profile", "balanced");

        report.Succeeded.Should().BeTrue();
        report.AverageFrequencyDeltaMHz.Should().Be(0);
        report.Notes.Should().Contain(note => note.Contains("unchanged", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CpuFrequencyLoadSampler_ShouldAggregateReadingsAcrossSampleWindow()
    {
        var readCount = 0;
        var sampler = new CpuFrequencyLoadSampler(
            () =>
            {
                readCount++;
                return readCount == 1
                    ? CreateTelemetry(new CpuFrequencyReading("cpu0", 1000, "first"))
                    : CreateTelemetry(new CpuFrequencyReading("cpu0", 2000, "second"));
            },
            new PerformanceEffectVerificationOptions(
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(10),
                0));

        var summary = sampler.Sample();

        readCount.Should().BeGreaterThan(1);
        summary.Count.Should().Be(readCount);
        summary.AverageMHz.Should().BeGreaterThan(1000);
        summary.MaxMHz.Should().Be(2000);
        summary.Source.Should().Be("first, second");
    }

    private static PerformanceEffectVerificationOptions CreateOptions() =>
        new(TimeSpan.Zero, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), 0);

    private static SystemTelemetry CreateTelemetry(params CpuFrequencyReading[] cpuFrequencies) =>
        new(
            "test",
            "Test CPU",
            2,
            16,
            8,
            cpuFrequencies,
            [],
            [],
            []);
}
