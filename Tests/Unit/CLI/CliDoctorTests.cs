using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.CLI;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Trait("Category", TestCategories.Unit)]
public sealed class CliDoctorTests
{
    [Fact]
    public void Inspect_MissingIntegrations_ReportsCliOffAndNoPipe()
    {
        var root = CreateTempRoot();
        var report = CliDoctor.Inspect(root, _ => false);

        report.CliSetting.Should().BeFalse();
        report.Pipe.Should().BeFalse();
        report.Ready.Should().BeFalse();
        report.AppData.Should().Be(root);
        report.Hint.Should().Contain("Settings");
        JsonSerializer.Serialize(new
        {
            ok = true,
            command = "doctor",
            appData = report.AppData,
            cliSetting = report.CliSetting,
            pipe = report.Pipe,
            ready = report.Ready,
            hint = report.Hint,
        }).Should().Contain("\"cliSetting\":false");
    }

    [Fact]
    public void Inspect_CliEnabledWithoutPipe_HintsRestart()
    {
        var root = CreateTempRoot();
        File.WriteAllText(Path.Combine(root, CliDoctor.IntegrationsFileName), """{"HWiNFO":false,"CLI":true}""");

        var report = CliDoctor.Inspect(root, _ => false);

        report.CliSetting.Should().BeTrue();
        report.Pipe.Should().BeFalse();
        report.Ready.Should().BeFalse();
        report.Hint.Should().Contain("pipe");
    }

    [Fact]
    public void Inspect_CliEnabledWithPipe_IsReady()
    {
        var root = CreateTempRoot();
        File.WriteAllText(Path.Combine(root, CliDoctor.IntegrationsFileName), """{"CLI":true}""");

        var report = CliDoctor.Inspect(root, _ => true);

        report.Ready.Should().BeTrue();
        report.Hint.Should().Contain("ready");
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-cli-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
