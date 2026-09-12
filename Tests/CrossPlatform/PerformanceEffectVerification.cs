using System.Diagnostics;

internal sealed record PerformanceEffectVerificationReport(
    string ControlId,
    string RequestedValue,
    HardwareControlSetResult ControlResult,
    CpuFrequencySummary BeforeCpuFrequency,
    CpuFrequencySummary AfterCpuFrequency,
    double? AverageFrequencyDeltaMHz,
    TimeSpan SampleDuration,
    int LoadWorkerCount,
    string[] Notes)
{
    public bool Succeeded => ControlResult.Succeeded;
}

internal sealed record PerformanceEffectVerificationOptions(
    TimeSpan StabilizationDelay,
    TimeSpan LoadSampleDuration,
    TimeSpan SampleInterval,
    int LoadWorkerCount)
{
    public static PerformanceEffectVerificationOptions Default =>
        new(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(250),
            Math.Clamp(Environment.ProcessorCount, 1, 4));
}

internal sealed record CpuFrequencySummary(
    int Count,
    double? AverageMHz,
    double? MinMHz,
    double? MaxMHz,
    string Source)
{
    public static CpuFrequencySummary From(IReadOnlyCollection<CpuFrequencyReading> readings)
    {
        if (readings.Count == 0)
            return new CpuFrequencySummary(0, null, null, null, string.Empty);

        var values = readings.Select(reading => reading.MHz).ToArray();
        var source = string.Join(
            ", ",
            readings
                .Select(reading => reading.Source)
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase));

        return new CpuFrequencySummary(
            values.Length,
            Math.Round(values.Average(), 1),
            Math.Round(values.Min(), 1),
            Math.Round(values.Max(), 1),
            source);
    }
}

internal sealed class PerformanceEffectVerifier(
    Func<CpuFrequencySummary> sampleCpuFrequency,
    Func<string, string, HardwareControlSetResult> setControl,
    Action waitBetweenSamples,
    PerformanceEffectVerificationOptions options)
{
    public PerformanceEffectVerificationReport Verify(string controlId, string value)
    {
        var before = sampleCpuFrequency();
        var result = setControl(controlId, value);
        waitBetweenSamples();
        var after = sampleCpuFrequency();
        var notes = BuildNotes(result, before, after);

        return new PerformanceEffectVerificationReport(
            result.ControlId,
            value,
            result,
            before,
            after,
            before.AverageMHz is null || after.AverageMHz is null
                ? null
                : Math.Round(after.AverageMHz.Value - before.AverageMHz.Value, 1),
            options.LoadSampleDuration,
            Math.Max(0, options.LoadWorkerCount),
            notes);
    }

    private static string[] BuildNotes(
        HardwareControlSetResult result,
        CpuFrequencySummary before,
        CpuFrequencySummary after)
    {
        var notes = new List<string>();
        if (!result.Succeeded)
            notes.Add($"Control write failed: {result.Detail}");

        if (before.Count == 0)
            notes.Add("No CPU frequency readings were available before the control change.");

        if (after.Count == 0)
            notes.Add("No CPU frequency readings were available after the control change.");

        if (result.Succeeded && before.AverageMHz is not null && after.AverageMHz is not null)
        {
            var delta = Math.Round(after.AverageMHz.Value - before.AverageMHz.Value, 1);
            if (Math.Abs(delta) < 1)
                notes.Add("Average CPU frequency was unchanged in the sampled load window; run under a longer workload-specific scenario for stronger evidence.");
        }

        return notes.ToArray();
    }
}

internal sealed class CpuFrequencyLoadSampler(
    Func<SystemTelemetry> readTelemetry,
    PerformanceEffectVerificationOptions options)
{
    public CpuFrequencySummary Sample()
    {
        var readings = new List<CpuFrequencyReading>();
        var duration = options.LoadSampleDuration <= TimeSpan.Zero
            ? TimeSpan.Zero
            : options.LoadSampleDuration;
        var interval = options.SampleInterval <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(250)
            : options.SampleInterval;

        if (duration == TimeSpan.Zero)
        {
            readings.AddRange(readTelemetry().CpuFrequencies);
            return CpuFrequencySummary.From(readings);
        }

        using var cancellation = new CancellationTokenSource();
        var workers = StartLoadWorkers(options.LoadWorkerCount, cancellation.Token);
        var sampleCount = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (stopwatch.Elapsed < duration || sampleCount < 2)
            {
                sampleCount++;
                readings.AddRange(readTelemetry().CpuFrequencies);
                var remaining = duration - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero && sampleCount >= 2)
                    break;

                Thread.Sleep(remaining <= TimeSpan.Zero || remaining > interval ? interval : remaining);
            }
        }
        finally
        {
            cancellation.Cancel();
            Task.WaitAll(workers, TimeSpan.FromSeconds(1));
        }

        return CpuFrequencySummary.From(readings);
    }

    private static Task[] StartLoadWorkers(int workerCount, CancellationToken cancellationToken) =>
        Enumerable
            .Range(0, Math.Max(0, workerCount))
            .Select(_ => Task.Run(() => RunCpuLoad(cancellationToken)))
            .ToArray();

    private static void RunCpuLoad(CancellationToken cancellationToken)
    {
        var value = 0.0;
        while (!cancellationToken.IsCancellationRequested)
        {
            for (var i = 1; i <= 4096; i++)
                value += Math.Sqrt(i + value % 31);

            if (value > 1_000_000)
                value = 0;
        }

        GC.KeepAlive(value);
    }
}
