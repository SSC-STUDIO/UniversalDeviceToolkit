using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;
using Moq;

namespace UniversalDeviceToolkit.Tests.Features;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public class PowerModeFeatureTests : UnitTestBase
{
    [Fact]
    public void PowerModeState_ShouldIncludeExtremeAndGodMode()
    {
        var states = Enum.GetValues<PowerModeState>();

        states.Should().HaveCount(5);
        states.Should().Contain(PowerModeState.Quiet);
        states.Should().Contain(PowerModeState.Balance);
        states.Should().Contain(PowerModeState.Performance);
        states.Should().Contain(PowerModeState.Extreme);
        states.Should().Contain(PowerModeState.GodMode);
    }

    [Fact]
    public void PowerModeState_Values_ShouldBeCorrect()
    {
        ((int)PowerModeState.Quiet).Should().Be(0);
        ((int)PowerModeState.Balance).Should().Be(1);
        ((int)PowerModeState.Performance).Should().Be(2);
        ((int)PowerModeState.GodMode).Should().Be(254);
    }

    [Fact]
    public void PowerModeUnavailableWithoutACException_ShouldContainPowerMode()
    {
        var exception = new PowerModeUnavailableWithoutACException(PowerModeState.Performance);

        exception.PowerMode.Should().Be(PowerModeState.Performance);
        exception.Message.Should().Contain("Performance");
    }

    [Fact]
    public async Task IsSupportedAsync_WhenResolvedAsInterface_ShouldUseLenovoPowerModeAvailability()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DE",
                Model = "Legion Pro 7 16IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = (IFeature<PowerModeState>)new TestPowerModeFeature(
                () => Task.FromResult(PowerModeState.Balance),
                wmiSupported: false);

            var result = await feature.IsSupportedAsync();

            result.Should().BeTrue();
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task IsSupportedAsync_WhenBasicNonLenovoDeviceReportsPowerModes_ShouldReturnFalse()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "Dell Inc.",
                MachineType = "0000",
                Model = "Alienware m18 R2",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new TestPowerModeFeature(
                () => Task.FromResult(PowerModeState.Balance),
                wmiSupported: false);

            var result = await feature.IsSupportedAsync();

            result.Should().BeFalse();
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task SetStateAsync_WhenBasicNonLenovoDeviceReportsPowerModes_ShouldRejectBeforeLenovoWmiWrite()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "Dell Inc.",
                MachineType = "0000",
                Model = "Alienware m18 R2",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new TestPowerModeFeature(
                () => throw new InvalidOperationException("Should not read Lenovo WMI."),
                wmiSupported: false);

            var act = () => feature.SetStateAsync(PowerModeState.Performance);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Power mode switching is not supported on this device.");
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenMachineReportsExtreme_ShouldNotIncludeExtreme()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DE",
                Model = "Legion Pro 7 16IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.Extreme],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new LenovoPowerModeFeature(
                null!,
                null!,
                null!,
                null!,
                null!);

            var states = await feature.GetAllStatesAsync();

            states.Should().NotContain(PowerModeState.Extreme);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenSupportedPowerModesIsSparse_ShouldStillIncludeStandardModes()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DE",
                Model = "Legion Pro 7 16IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Balance],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new LenovoPowerModeFeature(
                null!,
                null!,
                null!,
                null!,
                null!);

            var states = await feature.GetAllStatesAsync();

            states.Should().Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenMachinePropertiesSupportExtraModes_ShouldIncludeGodModeOnly()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DE",
                Model = "Legion Pro 7 16IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Extreme, PowerModeState.GodMode],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData
                {
                    SupportsGodModeV2 = true
                }
            });

            var feature = new LenovoPowerModeFeature(
                null!,
                null!,
                null!,
                null!,
                null!);

            var states = await feature.GetAllStatesAsync();

            states.Should().Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode]);
            states.Should().NotContain(PowerModeState.Extreme);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenCapabilityDataExposesGodModeFnQSwitchable_ShouldIncludeGodMode()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DF",
                Model = "Legion Y9000P IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance],
                Features = new MachineInformation.FeatureData(
                    MachineInformation.FeatureData.SourceType.CapabilityData,
                    [CapabilityID.GodModeFnQSwitchable]),
                Properties = new MachineInformation.PropertyData
                {
                    SupportsGodModeV2 = true
                }
            });

            var feature = new LenovoPowerModeFeature(
                null!,
                null!,
                null!,
                null!,
                null!);

            var states = await feature.GetAllStatesAsync();

            states.Should().Contain(PowerModeState.GodMode);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenSupportedPowerModesIsNull_ShouldStillReturnStandardModes()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DE",
                Model = "Legion Pro 7 16IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = null!,
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new LenovoPowerModeFeature(
                null!,
                null!,
                null!,
                null!,
                null!);

            var states = await feature.GetAllStatesAsync();

            states.Should().Contain([PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance]);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetStateAsync_WhenRuntimeReturnsExtreme_ShouldMapToPerformanceForUi()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DF",
                Model = "Legion Y9000P IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.Extreme],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new TestPowerModeFeature(() => Task.FromResult(PowerModeState.Extreme));

            var state = await feature.GetStateAsync();

            // Extreme is not listed in GetAllStatesAsync; UI must still get a selectable value.
            state.Should().Be(PowerModeState.Performance);
            (await feature.GetAllStatesAsync()).Should().Contain(state);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetStateAsync_WhenRuntimeReturnsUndefinedValue_ShouldFallBackToListedMode()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DF",
                Model = "Legion Y9000P IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            // Cast an undefined enum ordinal (not Quiet/Balance/Performance/Extreme/GodMode).
            var feature = new TestPowerModeFeature(() => Task.FromResult((PowerModeState)7));

            var state = await feature.GetStateAsync();

            state.Should().Be(PowerModeState.Balance);
            (await feature.GetAllStatesAsync()).Should().Contain(state);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetStateAsync_WhenRuntimeReadFails_ShouldFallBackToBalance()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DF",
                Model = "Legion Y9000P IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var feature = new TestPowerModeFeature(() => throw new InvalidOperationException("WMI access denied"));

            var state = await feature.GetStateAsync();

            state.Should().Be(PowerModeState.Balance);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    [Fact]
    public async Task GetStateAsync_WhenRuntimeReadFailsAfterSuccess_ShouldUseLastKnownState()
    {
        ResetCompatibilityCache();

        try
        {
            SetMachineInformation(new MachineInformation
            {
                Vendor = "LENOVO",
                MachineType = "83DF",
                Model = "Legion Y9000P IRX9",
                SerialNumber = "TEST",
                SupportedPowerModes = [],
                Features = MachineInformation.FeatureData.Unknown,
                Properties = new MachineInformation.PropertyData()
            });

            var invocationCount = 0;
            var feature = new TestPowerModeFeature(() =>
            {
                invocationCount++;
                return invocationCount switch
                {
                    1 => Task.FromResult(PowerModeState.Performance),
                    _ => throw new InvalidOperationException("WMI access denied"),
                };
            });

            var firstState = await feature.GetStateAsync();
            var secondState = await feature.GetStateAsync();

            firstState.Should().Be(PowerModeState.Performance);
            secondState.Should().Be(PowerModeState.Performance);
        }
        finally
        {
            ResetCompatibilityCache();
        }
    }

    private static void SetMachineInformation(MachineInformation machineInformation)
    {
        var lazy = new Lazy<Task<MachineInformation>>(() => Task.FromResult(machineInformation));
        typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, lazy);
    }

    private static void ResetCompatibilityCache()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        var lazyField = typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static);
        if (lazyField != null)
        {
            var method = typeof(Compatibility).GetMethod("GetMachineInformationInternalAsync", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                var del = Delegate.CreateDelegate(typeof(Func<Task<MachineInformation>>), method);
                var newLazy = Activator.CreateInstance(typeof(Lazy<Task<MachineInformation>>), [del, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication]);
                lazyField.SetValue(null, newLazy);
            }
        }
        typeof(Compatibility).GetField("_isCompatible", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    }

    private sealed class TestPowerModeFeature(Func<Task<PowerModeState>> readState, bool wmiSupported = true) : LenovoPowerModeFeature(
        null!,
        null!,
        null!,
        null!,
        null!)
    {
        internal override Task<PowerModeState> ReadStateCoreAsync(CancellationToken cancellationToken = default) => readState();

        internal override Task<bool> IsWmiSupportedAsync(CancellationToken cancellationToken = default) => Task.FromResult(wmiSupported);
    }
}


[Trait("Category", TestCategories.Unit)]
public class BatteryFeatureTests : UnitTestBase
{
    [Fact]
    public void BatteryState_ShouldHaveThreeStates()
    {
        var states = Enum.GetValues<BatteryState>();

        states.Should().HaveCount(3);
        states.Should().Contain(BatteryState.Conservation);
        states.Should().Contain(BatteryState.Normal);
        states.Should().Contain(BatteryState.RapidCharge);
    }

    [Fact]
    public void BatteryState_Values_ShouldBeCorrect()
    {
        ((int)BatteryState.Conservation).Should().Be(0);
        ((int)BatteryState.Normal).Should().Be(1);
        ((int)BatteryState.RapidCharge).Should().Be(2);
    }
}


[Trait("Category", TestCategories.Unit)]
public class IFeatureInterfaceTests : UnitTestBase
{
    [Fact]
    public void IFeature_ShouldHaveCorrectMethods()
    {
        var methodNames = new[]
        {
            nameof(IFeature<BatteryState>.IsSupportedAsync),
            nameof(IFeature<BatteryState>.GetAllStatesAsync),
            nameof(IFeature<BatteryState>.GetStateAsync),
            nameof(IFeature<BatteryState>.SetStateAsync)
        };

        foreach (var methodName in methodNames)
        {
            typeof(IFeature<BatteryState>).GetMethod(methodName).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task IFeature_MockImplementation_ShouldWorkCorrectly()
    {
        var mockFeature = new Mock<IFeature<PowerModeState>>();

        mockFeature
            .Setup(f => f.IsSupportedAsync())
            .ReturnsAsync(true);

        mockFeature
            .Setup(f => f.GetAllStatesAsync())
            .ReturnsAsync(new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance });

        mockFeature
            .Setup(f => f.GetStateAsync())
            .ReturnsAsync(PowerModeState.Balance);

        var isSupported = await mockFeature.Object.IsSupportedAsync();
        var states = await mockFeature.Object.GetAllStatesAsync();
        var currentState = await mockFeature.Object.GetStateAsync();

        isSupported.Should().BeTrue();
        states.Should().HaveCount(3);
        currentState.Should().Be(PowerModeState.Balance);
    }
}


[Trait("Category", TestCategories.Unit)]
public class PowerModeStateExtensionsTests : UnitTestBase
{
    [Fact]
    public void Next_ShouldReturnCorrectNextState()
    {
        PowerModeState.Quiet.Next().Should().Be(PowerModeState.Balance);
        PowerModeState.Balance.Next().Should().Be(PowerModeState.Performance);
        PowerModeState.Performance.Next().Should().Be(PowerModeState.GodMode);
        PowerModeState.GodMode.Next().Should().Be(PowerModeState.Quiet);
    }

    [Fact]
    public void Previous_ShouldReturnCorrectPreviousState()
    {
        PowerModeState.Quiet.Previous().Should().Be(PowerModeState.GodMode);
        PowerModeState.Balance.Previous().Should().Be(PowerModeState.Quiet);
        PowerModeState.Performance.Previous().Should().Be(PowerModeState.Balance);
        PowerModeState.GodMode.Previous().Should().Be(PowerModeState.Performance);
    }
}

public static class PowerModeStateExtensions
{
    public static PowerModeState Next(this PowerModeState state)
    {
        return state switch
        {
            PowerModeState.Quiet => PowerModeState.Balance,
            PowerModeState.Balance => PowerModeState.Performance,
            PowerModeState.Performance => PowerModeState.GodMode,
            PowerModeState.GodMode => PowerModeState.Quiet,
            _ => PowerModeState.Balance
        };
    }

    public static PowerModeState Previous(this PowerModeState state)
    {
        return state switch
        {
            PowerModeState.Quiet => PowerModeState.GodMode,
            PowerModeState.Balance => PowerModeState.Quiet,
            PowerModeState.Performance => PowerModeState.Balance,
            PowerModeState.GodMode => PowerModeState.Performance,
            _ => PowerModeState.Balance
        };
    }
}
