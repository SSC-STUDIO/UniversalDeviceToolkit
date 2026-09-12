using System;
using System.IO;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class LampArraySettingsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _previousOverride;

    public LampArraySettingsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "llt-lamparray-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _previousOverride = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _tempRoot);
    }

    public void Dispose()
    {
        if (_previousOverride is null)
            Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, null);
        else
            Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _previousOverride);

        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void Store_ShouldDefaultToNoExplicitDefaultEffect()
    {
        var settings = new LampArraySettings();

        settings.Store.DefaultEffect.Should().BeNull();
        settings.Store.PerLampEffects.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SynchronizeStore_ShouldRoundTripNullDefaultEffect()
    {
        var settings = new LampArraySettings();
        settings.Store.Brightness = 0.45;
        settings.Store.Speed = 1.75;
        settings.Store.SmoothTransition = false;
        settings.Store.DefaultEffect = null;
        settings.Store.PerLampEffects[7] = new LampArraySettings.LampEffectConfig
        {
            EffectType = LampEffectType.Static,
            Parameters =
            {
                ["Color"] = "255,1,2,3"
            }
        };

        settings.SynchronizeStore();
        settings.InvalidateCache();

        var reloaded = settings.Store;

        reloaded.DefaultEffect.Should().BeNull();
        reloaded.Brightness.Should().Be(0.45);
        reloaded.Speed.Should().Be(1.75);
        reloaded.SmoothTransition.Should().BeFalse();
        reloaded.PerLampEffects.Should().ContainKey(7);
        reloaded.PerLampEffects[7].EffectType.Should().Be(LampEffectType.Static);
        reloaded.PerLampEffects[7].Parameters["Color"].ToString().Should().Be("255,1,2,3");
    }

    [Fact]
    public void ImportFromFile_ShouldNormalizeMissingPerLampEffects()
    {
        var path = Path.Combine(_tempRoot, "import.json");
        File.WriteAllText(path,
            """
            {
              "Brightness": 0.6,
              "Speed": 0.9,
              "SmoothTransition": true,
              "DefaultEffect": null,
              "PerLampEffects": null
            }
            """);

        var settings = new LampArraySettings();

        settings.ImportFromFile(path);

        settings.Store.DefaultEffect.Should().BeNull();
        settings.Store.PerLampEffects.Should().NotBeNull().And.BeEmpty();
        settings.Store.Brightness.Should().Be(0.6);
        settings.Store.Speed.Should().Be(0.9);
    }
}
