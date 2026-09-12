using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Unit)]
public class HardwareStateRecoveryServiceTests : IDisposable
{
    private readonly string _tempAppDataRoot;

    public HardwareStateRecoveryServiceTests()
    {
        _tempAppDataRoot = Path.Combine(Path.GetTempPath(), "udt_hwstate_" + Guid.NewGuid().ToString("N"));
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
    public void TryResetHardware_NoIoCComponents_AllStepsReportSkipped()
    {
        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryResetHardware(out var report);

        ok.Should().BeTrue();
        report.Should().Contain("god-mode-to-balance").And.Contain("skipped (component not initialized)");
        report.Should().Contain("rgb-keyboard-backlight").And.Contain("skipped (component not initialized)");
        report.Should().Contain("spectrum-keyboard-backlight").And.Contain("skipped (component not initialized)");
        report.Should().Contain("WhiteKeyboardLenovoLightingBacklightFeature").And.Contain("skipped");
        report.Should().Contain("PortsBacklightFeature").And.Contain("skipped");
        report.Should().Contain("experimental-gpu-working-mode").And.Contain("skipped");
        report.Should().Contain("processor-min-state").And.Contain("skipped (optional");
    }

    [Fact]
    public void TryResetHardware_WithRestoreProcessorMin_MentionsActivePlanMutation()
    {
        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        // May succeed or partially fail depending on Win32 power APIs; must not throw.
        _ = service.TryResetHardware(out var report, restoreProcessorMinState: true);

        report.Should().Contain("god-mode-to-balance");
        report.Should().Contain("processor-min-state");
        report.Should().NotContain("skipped (optional");
    }

    [Fact]
    public void TryResetNetwork_NoArgsFile_ReportsSkipped()
    {
        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryResetNetwork(out var report);

        ok.Should().BeTrue();
        report.Should().Contain("args.txt").And.Contain("skipped (file not present)");
    }

    [Fact]
    public void TryResetNetwork_RemovesProxyArgsOnly()
    {
        var argsPath = Path.Combine(Folders.AppData, "args.txt");
        File.WriteAllLines(argsPath, new[]
        {
            "--minimized",
            "--proxy-url http://example.com",
            "--proxy-username user",
            "--disable-tray-tooltip",
            "--proxy-allow-all-certs",
        });

        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryResetNetwork(out var report);

        ok.Should().BeTrue();
        report.Should().Contain("args.txt");
        report.Should().Contain("proxy-related entries removed");

        var remaining = File.ReadAllLines(argsPath);
        remaining.Should().Contain("--minimized");
        remaining.Should().Contain("--disable-tray-tooltip");
        remaining.Should().NotContain(arg => arg.Contains("--proxy"));
    }

    [Fact]
    public void TryBackupCorruptedConfig_NoSource_ReturnsSuccessAndReportsPath()
    {
        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryBackupCorruptedConfig(out var report);

        ok.Should().BeTrue();
        report.Should().Contain("settings.json");
        report.Should().Contain("not present (nothing to back up)");
    }

    [Fact]
    public void TryBackupCorruptedConfig_MovesSourceToTimestampedBackup()
    {
        var sourcePath = Path.Combine(Folders.AppData, "settings.json");
        File.WriteAllText(sourcePath, "{\"corrupted\": true}");

        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryBackupCorruptedConfig(out var report);

        ok.Should().BeTrue();
        report.Should().Contain("backup:");
        report.Should().Contain("status: OK");

        File.Exists(sourcePath).Should().BeFalse();

        var backups = Directory.GetFiles(Folders.AppData, "settings.json.bak.*");
        backups.Should().ContainSingle();
        backups[0].Should().StartWith(Path.Combine(Folders.AppData, "settings.json.bak."));
    }

    [Fact]
    public void TryBackupFile_GenericFilename_DelegatesCorrectly()
    {
        var sourcePath = Path.Combine(Folders.AppData, "fan_curves.json");
        File.WriteAllText(sourcePath, "{}");

        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryBackupFile("fan_curves.json", out var report);

        ok.Should().BeTrue();
        File.Exists(sourcePath).Should().BeFalse();

        var backups = Directory.GetFiles(Folders.AppData, "fan_curves.json.bak.*");
        backups.Should().ContainSingle();
    }

    [Fact]
    public void TryBackupFile_EmptyFilename_ReturnsFalse()
    {
        var console = new StringWriter();
        var impl = new HardwareStateRecoveryImplementation(_ => null, s => console.Write(s));
        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryBackupFile(string.Empty, out var report);

        ok.Should().BeFalse();
        report.Should().Contain("No filename supplied");
    }

    [Fact]
    public void TryResetHardware_TraceSink_CanBeObservedButIsOptional()
    {
        var console = new StringWriter();
        var traceSink = new List<string>();

        var impl = new HardwareStateRecoveryImplementation(
            tryResolve: _ => null,
            console: s => console.Write(s),
            trace: msg => traceSink.Add(msg));

        var service = new HardwareStateRecoveryService(impl);

        var ok = service.TryResetHardware(out _);

        ok.Should().BeTrue();
        traceSink.Should().NotBeNull();
    }
}
