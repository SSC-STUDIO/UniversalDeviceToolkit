using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers.GodMode;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Tests.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.Settings)]
public class GodModeControllerTests : UnitTestBase
{
    private TestGodModeControllerV1 _controllerV1 = null!;
    private TestGodModeControllerV2 _controllerV2 = null!;
    private GodModeController _controller = null!;
    private object? _originalMachineInformation;
    private object? _originalIsCompatible;

    protected override void Setup()
    {
        SettingsCleanupHelper.UseIsolatedAppData();
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.GodMode);
        BackupCompatibilityState();

        var v1Settings = new GodModeSettings();
        var v2Settings = new GodModeSettings();

        _controllerV1 = new TestGodModeControllerV1(v1Settings);
        _controllerV2 = new TestGodModeControllerV2(v2Settings);
        _controller = new GodModeController(_controllerV1, _controllerV2);

        SetCompatibility(supportsGodModeV1: true, supportsGodModeV2: false);
    }

    protected override void Cleanup()
    {
        RestoreCompatibilityState();
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.GodMode);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _controller.Should().NotBeNull();
    }

    [Fact]
    public void PresetChanged_Event_ShouldBeSubscribedToBothControllers()
    {
        var eventCount = 0;
        EventHandler<Guid> handler = (_, _) => eventCount++;

        _controller.PresetChanged += handler;

        _controllerV1.TriggerPresetChanged(Guid.NewGuid());
        _controllerV2.TriggerPresetChanged(Guid.NewGuid());

        eventCount.Should().Be(2);

        _controller.PresetChanged -= handler;
    }

    [Fact]
    public async Task NeedsVantageDisabledAsync_ShouldCallCorrectController()
    {
        var result = await _controller.NeedsVantageDisabledAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NeedsLegionZoneDisabledAsync_ShouldCallCorrectController()
    {
        var result = await _controller.NeedsLegionZoneDisabledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetActivePresetIdAsync_ShouldReturnCorrectId()
    {
        var expectedId = Guid.NewGuid();
        _controllerV1.SetStore(expectedId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [expectedId] = new() { Name = "Preset A" }
        });

        var result = await _controller.GetActivePresetIdAsync();

        result.Should().Be(expectedId);
    }

    [Fact]
    public async Task GetActivePresetNameAsync_ShouldReturnCorrectName()
    {
        const string expectedName = "Test Preset";
        var presetId = Guid.NewGuid();
        _controllerV1.SetStore(presetId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [presetId] = new() { Name = expectedName }
        });

        var result = await _controller.GetActivePresetNameAsync();

        result.Should().Be(expectedName);
    }

    [Fact]
    public async Task GetActivePresetNameAsync_WhenNoPreset_ShouldReturnNull()
    {
        var activeId = Guid.NewGuid();
        _controllerV1.SetStore(activeId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [Guid.NewGuid()] = new() { Name = "Different Preset" }
        });

        var result = await _controller.GetActivePresetNameAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetStateAsync_ShouldCallCorrectController()
    {
        var presetId = Guid.NewGuid();
        var state = new GodModeState
        {
            ActivePresetId = presetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(new Dictionary<Guid, GodModePreset>
            {
                [presetId] = new() { Name = "Saved Preset" }
            })
        };

        await _controller.SetStateAsync(state);

        var result = await _controller.GetActivePresetIdAsync();
        result.Should().Be(presetId);
    }

    [Fact]
    public async Task ApplyStateAsync_ShouldCallCorrectController()
    {
        await _controller.ApplyStateAsync();

        _controllerV1.ApplyCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetDefaultFanTableAsync_ShouldReturnFanTable()
    {
        var result = await _controller.GetDefaultFanTableAsync();

        result.GetTable().Should().Equal((ushort)1, (ushort)2, (ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7, (ushort)8, (ushort)9, (ushort)10);
    }

    [Fact]
    public async Task GetMinimumFanTableAsync_ShouldReturnFanTable()
    {
        var result = await _controller.GetMinimumFanTableAsync();

        result.GetTable().Should().Equal((ushort)1, (ushort)1, (ushort)1, (ushort)1, (ushort)1, (ushort)1, (ushort)1, (ushort)1, (ushort)3, (ushort)5);
    }

    [Fact]
    public async Task GetDefaultsInOtherPowerModesAsync_ShouldReturnDictionary()
    {
        var result = await _controller.GetDefaultsInOtherPowerModesAsync();

        result.Should().ContainKey(PowerModeState.Quiet);
    }

    [Fact]
    public async Task GetStateAsync_WhenStoreEmpty_ShouldCreateBasePowerModePresets()
    {
        var state = await _controller.GetStateAsync();

        state.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
        state.Presets.Values.Select(p => p.Name)
            .Should()
            .Contain([PowerModeState.Quiet.GetDisplayName(), PowerModeState.Balance.GetDisplayName(), PowerModeState.Performance.GetDisplayName()]);
        state.Presets[state.ActivePresetId].SourcePowerMode.Should().Be(PowerModeState.Performance);

        var storedState = await _controller.GetStateAsync();
        storedState.Presets.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetStateAsync_WhenStoreHasLegacyPreset_ShouldPreserveAndAddMissingBasePowerModePresets()
    {
        var legacyPresetId = Guid.NewGuid();
        _controllerV1.SetStore(legacyPresetId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [legacyPresetId] = new() { Name = "Saved Preset" }
        });

        var state = await _controller.GetStateAsync();

        state.ActivePresetId.Should().Be(legacyPresetId);
        state.Presets.Should().ContainKey(legacyPresetId);
        state.Presets[legacyPresetId].Name.Should().Be("Saved Preset");
        state.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
        state.Presets.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetStateAsync_WhenStoreHasGeneratedDefaultPreset_ShouldMigrateToBasePowerModePresets()
    {
        var generatedDefaultPresetId = Guid.NewGuid();
        _controllerV1.SetStore(generatedDefaultPresetId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [generatedDefaultPresetId] = new()
            {
                Name = "Default",
                CPULongTermPowerLimit = new StepperValue(2, 1, 10, 1, [], 2),
                FanFullSpeed = false,
                MinValueOffset = 0,
                MaxValueOffset = 0
            }
        });

        var state = await _controller.GetStateAsync();

        state.Presets.Should().HaveCount(3);
        state.Presets.Should().NotContainKey(generatedDefaultPresetId);
        state.Presets[state.ActivePresetId].SourcePowerMode.Should().Be(PowerModeState.Performance);
        state.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
    }

    [Fact]
    public async Task GetStateAsync_WhenStoreHasUserCustomPresetNamedDefault_ShouldPreserveCustomPreset()
    {
        var customPresetId = Guid.NewGuid();
        _controllerV1.SetStore(customPresetId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [customPresetId] = new()
            {
                Name = "Default",
                PowerMode = WindowsPowerMode.BestPerformance,
                CPULongTermPowerLimit = new StepperValue(2, 1, 10, 1, [], 2)
            }
        });

        var state = await _controller.GetStateAsync();

        state.ActivePresetId.Should().Be(customPresetId);
        state.Presets.Should().ContainKey(customPresetId);
        state.Presets[customPresetId].Name.Should().Be("Default");
        state.Presets[customPresetId].PowerMode.Should().Be(WindowsPowerMode.BestPerformance);
        state.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
        state.Presets.Should().HaveCount(4);
    }

    [Fact]
    public async Task SetStateAsync_WhenBasePresetWasDeleted_ShouldRestoreMissingBasePowerModePreset()
    {
        var state = await _controller.GetStateAsync();
        var performancePreset = state.Presets.Single(kv => kv.Value.SourcePowerMode == PowerModeState.Performance);
        var presets = state.Presets
            .Where(kv => kv.Key != performancePreset.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var activePresetId = presets.First().Key;

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = activePresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(presets)
        });

        var restoredState = await _controller.GetStateAsync();

        restoredState.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
    }

    [Fact]
    public async Task GetStateAsync_WhenBasePresetWasRenamedAsCustom_ShouldAddReplacementBasePreset()
    {
        var state = await _controller.GetStateAsync();
        var performancePreset = state.Presets.Single(kv => kv.Value.SourcePowerMode == PowerModeState.Performance);
        var presets = state.Presets.ToDictionary(kv => kv.Key, kv => kv.Value);
        presets[performancePreset.Key] = performancePreset.Value with
        {
            Name = "My Performance",
            SourcePowerMode = null
        };

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = performancePreset.Key,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(presets)
        });

        var restoredState = await _controller.GetStateAsync();

        restoredState.Presets[performancePreset.Key].Name.Should().Be("My Performance");
        restoredState.Presets[performancePreset.Key].SourcePowerMode.Should().BeNull();
        restoredState.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
        restoredState.Presets.Values.Select(p => p.Name)
            .Should()
            .Contain(PowerModeState.Performance.GetDisplayName());
    }

    [Fact]
    public async Task SetStateAsync_WhenStateHasCustomPreset_ShouldPersistCustomPresetAndKeepItActive()
    {
        var state = await _controller.GetStateAsync();
        var performancePreset = state.Presets.Single(kv => kv.Value.SourcePowerMode == PowerModeState.Performance);
        var customPresetId = Guid.NewGuid();
        var presets = state.Presets.ToDictionary(kv => kv.Key, kv => kv.Value);
        presets[customPresetId] = performancePreset.Value with
        {
            Name = "Custom Performance",
            SourcePowerMode = null
        };

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = customPresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(presets)
        });

        var restoredState = await _controller.GetStateAsync();

        restoredState.ActivePresetId.Should().Be(customPresetId);
        restoredState.Presets.Should().ContainKey(customPresetId);
        restoredState.Presets[customPresetId].Name.Should().Be("Custom Performance");
        restoredState.Presets[customPresetId].SourcePowerMode.Should().BeNull();
        restoredState.Presets.Should().HaveCount(4);
        restoredState.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
    }

    [Fact]
    public async Task SetStateAsync_WhenCustomPresetIsAddedRenamedAndDeleted_ShouldPersistEachPresetListChange()
    {
        var originalState = await _controller.GetStateAsync();
        var sourcePreset = originalState.Presets.Single(kv => kv.Value.SourcePowerMode == PowerModeState.Performance);
        var customPresetId = Guid.NewGuid();
        var addedPresets = originalState.Presets.ToDictionary(kv => kv.Key, kv => kv.Value);
        addedPresets[customPresetId] = sourcePreset.Value with
        {
            Name = "Custom Performance",
            SourcePowerMode = null
        };

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = customPresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(addedPresets)
        });

        var persistedAfterAdd = await _controller.GetStateAsync();
        persistedAfterAdd.ActivePresetId.Should().Be(customPresetId);
        persistedAfterAdd.Presets.Should().ContainKey(customPresetId);
        persistedAfterAdd.Presets[customPresetId].Name.Should().Be("Custom Performance");

        var renamedPresets = persistedAfterAdd.Presets.ToDictionary(kv => kv.Key, kv => kv.Value);
        renamedPresets[customPresetId] = renamedPresets[customPresetId] with { Name = "Renamed Performance" };

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = customPresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(renamedPresets)
        });

        var persistedAfterRename = await _controller.GetStateAsync();
        persistedAfterRename.ActivePresetId.Should().Be(customPresetId);
        persistedAfterRename.Presets[customPresetId].Name.Should().Be("Renamed Performance");

        var remainingPresets = persistedAfterRename.Presets
            .Where(kv => kv.Key != customPresetId)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var nextActivePresetId = remainingPresets
            .OrderBy(kv => kv.Value.Name)
            .Select(kv => kv.Key)
            .First();

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = nextActivePresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(remainingPresets)
        });

        var persistedAfterDelete = await _controller.GetStateAsync();
        persistedAfterDelete.ActivePresetId.Should().Be(nextActivePresetId);
        persistedAfterDelete.Presets.Should().NotContainKey(customPresetId);
        persistedAfterDelete.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
    }

    [Fact]
    public async Task SetStateAsync_AfterGeneratedDefaultMigration_ShouldPersistNewCustomPresetAndKeepItVisible()
    {
        var generatedDefaultPresetId = Guid.NewGuid();
        _controllerV1.SetStore(generatedDefaultPresetId, new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>
        {
            [generatedDefaultPresetId] = new()
            {
                Name = "Default",
                CPULongTermPowerLimit = new StepperValue(2, 1, 10, 1, [], 2),
                FanFullSpeed = false,
                MinValueOffset = 0,
                MaxValueOffset = 0
            }
        });

        var migratedState = await _controller.GetStateAsync();
        var performancePreset = migratedState.Presets.Single(kv => kv.Value.SourcePowerMode == PowerModeState.Performance);
        var customPresetId = Guid.NewGuid();
        var presets = migratedState.Presets.ToDictionary(kv => kv.Key, kv => kv.Value);
        presets[customPresetId] = performancePreset.Value with
        {
            Name = "Added Performance Mode",
            SourcePowerMode = null
        };

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = customPresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(presets)
        });

        var persistedState = await _controller.GetStateAsync();

        persistedState.ActivePresetId.Should().Be(customPresetId);
        persistedState.Presets.Should().ContainKey(customPresetId);
        persistedState.Presets[customPresetId].Name.Should().Be("Added Performance Mode");
        persistedState.Presets.Should().NotContainKey(generatedDefaultPresetId);
        persistedState.Presets.Should().HaveCount(4);
        persistedState.Presets.Values.Select(p => p.SourcePowerMode)
            .Should()
            .Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
    }

    [Fact]
    public async Task SetStateAsync_WhenStateHasCustomPresetOnV2_ShouldPersistCustomPresetAndKeepItActive()
    {
        SetCompatibility(supportsGodModeV1: false, supportsGodModeV2: true);

        var state = await _controller.GetStateAsync();
        var customPresetId = Guid.NewGuid();
        var presets = state.Presets.ToDictionary(kv => kv.Key, kv => kv.Value);
        var activePreset = presets[state.ActivePresetId];
        presets[customPresetId] = activePreset with
        {
            Name = "Custom V2 Performance",
            SourcePowerMode = null
        };

        await _controller.SetStateAsync(new GodModeState
        {
            ActivePresetId = customPresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(presets)
        });

        var restoredState = await _controller.GetStateAsync();

        restoredState.ActivePresetId.Should().Be(customPresetId);
        restoredState.Presets.Should().ContainKey(customPresetId);
        restoredState.Presets[customPresetId].Name.Should().Be("Custom V2 Performance");
        restoredState.Presets[customPresetId].SourcePowerMode.Should().BeNull();
        restoredState.Presets.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task NeedsVantageDisabledAsync_WhenCompatibilityOnlyReportsGodModeV3_ShouldUseV2Controller()
    {
        SetCompatibility(supportsGodModeV1: false, supportsGodModeV2: false, supportsGodModeV3: true, supportsGodModeV4: false);

        var result = await _controller.NeedsVantageDisabledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreDefaultsInOtherPowerModeAsync_ShouldCallCorrectController()
    {
        var powerMode = PowerModeState.Quiet;
        await _controller.RestoreDefaultsInOtherPowerModeAsync(powerMode);

        _controllerV1.RestoreCalls.Should().Be(1);
        _controllerV1.LastRestoreState.Should().Be(powerMode);
    }

    private void BackupCompatibilityState()
    {
        _originalMachineInformation = GetCompatibilityField("_machineInformationLazy").GetValue(null);
        _originalIsCompatible = GetCompatibilityField("_isCompatible").GetValue(null);
    }

    private void RestoreCompatibilityState()
    {
        GetCompatibilityField("_machineInformationLazy").SetValue(null, _originalMachineInformation);
        GetCompatibilityField("_isCompatible").SetValue(null, _originalIsCompatible);
    }

    private static void SetCompatibility(bool supportsGodModeV1, bool supportsGodModeV2, bool supportsGodModeV3 = false, bool supportsGodModeV4 = false)
    {
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "82XX",
            Model = "16IRX",
            SupportedPowerModes = new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode },
            Properties = new MachineInformation.PropertyData
            {
                SupportsGodModeV1 = supportsGodModeV1,
                SupportsGodModeV2 = supportsGodModeV2,
                SupportsGodModeV3 = supportsGodModeV3,
                SupportsGodModeV4 = supportsGodModeV4
            }
        };

        var lazy = new Lazy<Task<MachineInformation>>(() => Task.FromResult(machineInformation));
        GetCompatibilityField("_machineInformationLazy").SetValue(null, lazy);
        GetCompatibilityField("_isCompatible").SetValue(null, true);
    }

    private static FieldInfo GetCompatibilityField(string name)
    {
        var field = typeof(Compatibility).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull();
        return field!;
    }

    private sealed class TestGodModeControllerV1 : GodModeControllerV1
    {
        private readonly GodModeSettings _settings;

        public int ApplyCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public PowerModeState? LastRestoreState { get; private set; }

        public TestGodModeControllerV1(GodModeSettings settings)
            : base(settings, new LegionZoneDisabler())
        {
            _settings = settings;
        }

        public override Task<bool> NeedsVantageDisabledAsync() => Task.FromResult(false);

        public override Task<bool> NeedsLegionZoneDisabledAsync() => Task.FromResult(true);

        public override Task ApplyStateAsync()
        {
            ApplyCalls++;
            return Task.CompletedTask;
        }

        public override Task<FanTable> GetMinimumFanTableAsync() => Task.FromResult(new FanTable([1, 1, 1, 1, 1, 1, 1, 1, 3, 5]));

        public override Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync() =>
            Task.FromResult(new Dictionary<PowerModeState, GodModeDefaults>
            {
                [PowerModeState.Quiet] = new() { CPULongTermPowerLimit = 1 },
                [PowerModeState.Balance] = new() { CPULongTermPowerLimit = 2 },
                [PowerModeState.Performance] = new() { CPULongTermPowerLimit = 3 }
            });

        public override Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state)
        {
            RestoreCalls++;
            LastRestoreState = state;
            return Task.CompletedTask;
        }

        protected override Task<GodModePreset> GetDefaultStateAsync() => Task.FromResult(new GodModePreset
        {
            Name = "Default V1",
            CPULongTermPowerLimit = new StepperValue(2, 1, 10, 1, [], 2)
        });

        public void TriggerPresetChanged(Guid presetId) => RaisePresetChanged(presetId);

        public void SetStore(Guid activePresetId, Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset> presets)
        {
            _settings.Store.ActivePresetId = activePresetId;
            _settings.Store.Presets = presets;
        }
    }

    private sealed class TestGodModeControllerV2 : GodModeControllerV2
    {
        public TestGodModeControllerV2(GodModeSettings settings)
            : base(settings, new VantageDisabler(), new LegionZoneDisabler())
        {
        }

        public override Task<bool> NeedsVantageDisabledAsync() => Task.FromResult(true);

        public override Task<bool> NeedsLegionZoneDisabledAsync() => Task.FromResult(true);

        public override Task ApplyStateAsync() => Task.CompletedTask;

        public override Task<FanTable> GetMinimumFanTableAsync() => Task.FromResult(new FanTable([1, 1, 1, 1, 1, 1, 1, 1, 3, 5]));

        public override Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync() =>
            Task.FromResult(new Dictionary<PowerModeState, GodModeDefaults>());

        public override Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state) => Task.CompletedTask;

        protected override Task<GodModePreset> GetDefaultStateAsync() => Task.FromResult(new GodModePreset { Name = "Default V2" });

        public void TriggerPresetChanged(Guid presetId) => RaisePresetChanged(presetId);
    }
}


[Trait("Category", TestCategories.Unit)]
public class FanTableTests : UnitTestBase
{
    [Fact]
    public void FanTable_DefaultConstructor_ShouldInitializeWithDefaults()
    {
        var fanTable = new FanTable();

        fanTable.Should().NotBeNull();
    }

    [Fact]
    public void FanTable_WithParameters_ShouldSetPropertiesCorrectly()
    {
        var fanTable = new FanTable
        {
            FSTM = 1,
            FSID = 0
        };

        fanTable.FSTM.Should().Be(1);
        fanTable.FSID.Should().Be(0);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValid10ElementArray_ShouldSucceed()
    {
        ushort[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var table = new FanTable(data);
        table.FSS0.Should().Be(1);
        table.FSS9.Should().Be(10);
    }

    [Fact]
    public void Constructor_WithInvalidLength_ShouldThrow()
    {
        ushort[] data = [1, 2, 3];
        var act = () => new FanTable(data);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldSetFSTMToOne()
    {
        ushort[] data = new ushort[10];
        var table = new FanTable(data);
        table.FSTM.Should().Be(1);
    }

    #endregion

    #region GetTable Tests

    [Fact]
    public void GetTable_ShouldReturn10ElementArray()
    {
        ushort[] data = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        var table = new FanTable(data);
        var result = table.GetTable();
        result.Should().HaveCount(10);
        result.Should().ContainInOrder(10, 20, 30, 40, 50, 60, 70, 80, 90, 100);
    }

    [Fact]
    public void GetTable_AfterRoundtrip_ShouldPreserveValues()
    {
        ushort[] data = [255, 128, 64, 32, 16, 8, 4, 2, 1, 0];
        var table = new FanTable(data);
        var result = table.GetTable();
        result.Should().ContainInOrder(data);
    }

    #endregion

    #region GetBytes Tests

    [Fact]
    public void GetBytes_ShouldReturn64ByteArray()
    {
        ushort[] data = new ushort[10];
        var table = new FanTable(data);
        var bytes = table.GetBytes();
        bytes.Length.Should().Be(64);
    }

    [Fact]
    public void GetBytes_FirstByteShouldBeFSTM()
    {
        ushort[] data = new ushort[10];
        var table = new FanTable(data);
        var bytes = table.GetBytes();
        bytes[0].Should().Be(1); // FSTM defaults to 1
    }

    #endregion
}


[Trait("Category", TestCategories.Unit)]
public class GodModeStateTests : UnitTestBase
{
    [Fact]
    public void GodModeState_DefaultConstructor_ShouldInitialize()
    {
        var state = new GodModeState();

        state.Should().NotBeNull();
    }

    [Fact]
    public void GodModeState_WithPresetId_ShouldSetCorrectly()
    {
        var presetId = Guid.NewGuid();
        var state = new GodModeState
        {
            ActivePresetId = presetId
        };

        state.ActivePresetId.Should().Be(presetId);
    }

    [Fact]
    public void Properties_ShouldRetainValues()
    {
        var id = Guid.NewGuid();
        var presetId = Guid.NewGuid();
        var preset = new GodModePreset { Name = "Test" };
        var presets = new ReadOnlyDictionary<Guid, GodModePreset>(
            new Dictionary<Guid, GodModePreset> { [presetId] = preset });

        var state = new GodModeState
        {
            ActivePresetId = id,
            Presets = presets
        };

        state.ActivePresetId.Should().Be(id);
        state.Presets.Should().HaveCount(1);
        state.Presets[presetId].Name.Should().Be("Test");
    }
}
