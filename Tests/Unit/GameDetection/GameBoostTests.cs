using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.GameDetection;
using Xunit;

namespace UniversalDeviceToolkit.Tests.GameDetection;

public class GameBoostTests
{
    [Fact]
    public void GameBoostSettingsStore_Defaults_ShouldBeValid()
    {
        var store = new GameBoostSettings.GameBoostSettingsStore();

        store.AutoGameBoost.Should().BeTrue();
        store.BoostGamePriority.Should().BeTrue();
        store.OptimizeCpuAffinity.Should().BeTrue();
        store.SuppressBackgroundProcesses.Should().BeTrue();
        store.MuteNotifications.Should().BeFalse();
        store.GamePowerPlanGuid.Should().BeNull();
        store.CustomGameProcesses.Should().BeEmpty();
        store.BackgroundWhitelist.Should().Contain("obs64");
        store.BackgroundWhitelist.Should().Contain("discord");
        store.BackgroundWhitelist.Should().Contain("steam");
    }

    [Fact]
    public void GameBoostSettingsStore_Normalize_NullInput_ReturnsNull()
    {
        var result = GameBoostSettings.Normalize(null);
        result.Should().BeNull();
    }

    [Fact]
    public void GameBoostSettingsStore_Normalize_TrimsAndLowercasesProcessNames()
    {
        var store = new GameBoostSettings.GameBoostSettingsStore
        {
            CustomGameProcesses = ["  MyGame.exe  ", "CYBERPUNK2077.EXE", ""],
            BackgroundWhitelist = ["  OBS64  ", "Discord.exe", "  "]
        };

        var normalized = GameBoostSettings.Normalize(store);

        normalized.Should().NotBeNull();
        normalized!.CustomGameProcesses.Should().Contain("mygame.exe");
        normalized.CustomGameProcesses.Should().Contain("cyberpunk2077.exe");
        normalized.CustomGameProcesses.Should().NotContain("");
        normalized.BackgroundWhitelist.Should().Contain("obs64");
        normalized.BackgroundWhitelist.Should().Contain("discord.exe");
    }

    [Fact]
    public void GameBoostSettingsStore_JsonRoundtrip_PreservesValues()
    {
        var original = new GameBoostSettings.GameBoostSettingsStore
        {
            AutoGameBoost = false,
            BoostGamePriority = true,
            OptimizeCpuAffinity = false,
            SuppressBackgroundProcesses = true,
            MuteNotifications = true,
            GamePowerPlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            CustomGameProcesses = ["game1", "game2"],
            BackgroundWhitelist = ["obs", "discord"]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<GameBoostSettings.GameBoostSettingsStore>(json);

        deserialized.Should().NotBeNull();
        deserialized!.AutoGameBoost.Should().BeFalse();
        deserialized.BoostGamePriority.Should().BeTrue();
        deserialized.OptimizeCpuAffinity.Should().BeFalse();
        deserialized.SuppressBackgroundProcesses.Should().BeTrue();
        deserialized.MuteNotifications.Should().BeTrue();
        deserialized.GamePowerPlanGuid.Should().Be("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        deserialized.CustomGameProcesses.Should().BeEquivalentTo(["game1", "game2"]);
        deserialized.BackgroundWhitelist.Should().BeEquivalentTo(["obs", "discord"]);
    }

    [Fact]
    public void CalculateOptimalGameAffinity_ReturnsNonNegative()
    {
        var affinity = GameBoostService.CalculateOptimalGameAffinity();
        affinity.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void CalculateOptimalBackgroundAffinity_ReturnsNonNegative()
    {
        var affinity = GameBoostService.CalculateOptimalBackgroundAffinity();
        affinity.Should().BeGreaterThanOrEqualTo(0);
    }
}
