using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Collection(TestCollections.ProcessState)]
public sealed class UpstreamCapabilityTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"udt-upstream-{Guid.NewGuid():N}");
    private readonly string? _previous;

    public UpstreamCapabilityTests()
    {
        Directory.CreateDirectory(_temp);
        _previous = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _temp);
    }

    [Fact]
    public async Task OrTrigger_MatchesWhenAnyChildMatchesAndShortCircuits()
    {
        var first = new FakeTrigger(false);
        var second = new FakeTrigger(true);
        var third = new FakeTrigger(true);
        var trigger = new OrAutomationPipelineTrigger([first, second, third]);

        (await trigger.IsMatchingEvent(new FakeEvent())).Should().BeTrue();
        first.EventCalls.Should().Be(1);
        second.EventCalls.Should().Be(1);
        third.EventCalls.Should().Be(0);
    }

    [Fact]
    public void OrTrigger_DeepCopyCopiesChildren()
    {
        var child = new FakeTrigger(true);
        var copy = (OrAutomationPipelineTrigger)new OrAutomationPipelineTrigger([child]).DeepCopy();
        copy.Triggers.Should().ContainSingle();
        copy.Triggers[0].Should().NotBeSameAs(child);
    }

    [Fact]
    public void SettingsBackup_ImportCreatesRollbackAndRestoresJson()
    {
        File.WriteAllText(Path.Combine(_temp, "settings.json"), "{\"value\":1}");
        var service = new SettingsBackupService();
        var backup = Path.Combine(_temp, "export.udtbackup");
        service.Export(backup);
        File.WriteAllText(Path.Combine(_temp, "settings.json"), "{\"value\":2}");
        // Extra file present only after export — import must remove it (replace, not merge).
        File.WriteAllText(Path.Combine(_temp, "orphan.json"), "{\"orphan\":true}");

        var rollback = service.Import(backup);

        File.ReadAllText(Path.Combine(_temp, "settings.json")).Should().Contain("1");
        File.Exists(Path.Combine(_temp, "orphan.json")).Should().BeFalse();
        File.Exists(rollback).Should().BeTrue();
    }

    [Fact]
    public void SettingsBackup_RejectsFutureFormat()
    {
        var backup = Path.Combine(_temp, "future.udtbackup");
        using (var archive = ZipFile.Open(backup, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("udt-settings-backup.json").Open()))
            writer.Write("{\"FormatVersion\":999,\"CreatedAtUtc\":\"2026-07-11T00:00:00Z\"}");

        new SettingsBackupService().Invoking(service => service.Import(backup)).Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SettingsBackup_RejectsManifestOnlyBackupWithoutDeletingExistingJson()
    {
        File.WriteAllText(Path.Combine(_temp, "settings.json"), "{\"value\":1}");
        var backup = Path.Combine(_temp, "empty.udtbackup");
        using (var archive = ZipFile.Open(backup, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("udt-settings-backup.json").Open()))
            writer.Write("{\"FormatVersion\":1,\"CreatedAtUtc\":\"2026-08-16T00:00:00Z\"}");

        new SettingsBackupService().Invoking(service => service.Import(backup)).Should().Throw<InvalidDataException>();
        File.ReadAllText(Path.Combine(_temp, "settings.json")).Should().Contain("1");
    }

    [Fact]
    public void SettingsBackup_ExportOverwritesExistingDestinationAtomically()
    {
        File.WriteAllText(Path.Combine(_temp, "settings.json"), "{\"value\":1}");
        var service = new SettingsBackupService();
        var backup = Path.Combine(_temp, "export.udtbackup");
        File.WriteAllText(backup, "stale-not-a-zip");

        service.Export(backup);

        ZipFile.OpenRead(backup).Dispose();
        File.Exists(backup + ".tmp").Should().BeFalse();
        using var archive = ZipFile.OpenRead(backup);
        archive.GetEntry("udt-settings-backup.json").Should().NotBeNull();
        archive.GetEntry("settings/settings.json").Should().NotBeNull();
    }

    [Fact]
    public void BatteryHealthAlertSettings_LoadStore_NormalizesInvalidThresholds()
    {
        File.WriteAllText(
            Path.Combine(_temp, "battery_health_alerts.json"),
            """{"AlertsEnabled":true,"LowHealthThreshold":250,"CriticalHealthThreshold":-5,"TemperatureThresholdC":200}""");

        var settings = new BatteryHealthAlertSettings();
        var store = settings.LoadStore();

        store.Should().NotBeNull();
        store!.LowHealthThreshold.Should().Be(BatteryHealthAlertSettings.DefaultLowHealthThreshold);
        store.CriticalHealthThreshold.Should().BeLessThan(store.LowHealthThreshold);
        store.TemperatureThresholdC.Should().Be(0);
    }

    [Fact]
    public void HardwareSensorSettings_LoadStore_ReplacesNullSectionArrays()
    {
        File.WriteAllText(
            Path.Combine(_temp, "hardware_sensors.json"),
            """{"SelectedGpuIsIgpu":false,"VisibleSections":null,"SectionOrder":null}""");

        var settings = new HardwareSensorSettings();
        var store = settings.LoadStore();

        store.Should().NotBeNull();
        store!.VisibleSections.Should().Equal("CPU", "Battery", "GPU");
        store.SectionOrder.Should().Equal("CPU", "Battery", "GPU");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _previous);
        try { Directory.Delete(_temp, true); } catch { }
    }

    private sealed class FakeEvent : IAutomationEvent;
    private sealed class FakeTrigger(bool result) : IAutomationPipelineTrigger
    {
        public int EventCalls { get; private set; }
        public string DisplayName => "fake";
        public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent) { EventCalls++; return Task.FromResult(result); }
        public Task<bool> IsMatchingState() => Task.FromResult(result);
        public void UpdateEnvironment(AutomationEnvironment environment) { }
        public IAutomationPipelineTrigger DeepCopy() => new FakeTrigger(result);
    }
}
