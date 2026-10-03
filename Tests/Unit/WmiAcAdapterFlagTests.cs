using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Management;
using Xunit;

namespace UniversalDeviceToolkit.Tests;

[Trait("Category", TestCategories.Unit)]
public sealed class WmiAcAdapterFlagTests
{
    [Fact]
    public async Task EmptyClassicRead_WhenCimReportsSufficient_DoesNotWarn()
    {
        var cimCalls = 0;

        var flags = await WMI.LenovoGameZoneData.ResolveAcAdapterFlagsAsync(
            () => Task.FromResult((Success: false, Value: 0)),
            () => Task.FromResult((Success: false, Value: 0)),
            () =>
            {
                cimCalls++;
                return Task.FromResult((Success: true, AcFitForOc: 1, PowerChargeMode: 1));
            });

        cimCalls.Should().Be(1);
        flags.AcFitForOc.Should().Be(1);
        flags.PowerChargeMode.Should().Be(1);
        Power.ResolvePowerAdapterStatus(
                adapterConnected: true,
                acFitForOc: flags.AcFitForOc == 1,
                chargingNormally: flags.PowerChargeMode == 1)
            .Should().Be(PowerAdapterStatus.Connected);
    }

    [Fact]
    public async Task ClassicLowFitFlag_IsKeptAndWarnsWhileConnected()
    {
        var cimCalls = 0;

        var flags = await WMI.LenovoGameZoneData.ResolveAcAdapterFlagsAsync(
            () => Task.FromResult((Success: true, Value: 0)),
            () => Task.FromResult((Success: true, Value: 1)),
            () =>
            {
                cimCalls++;
                return Task.FromResult((Success: true, AcFitForOc: 1, PowerChargeMode: 1));
            });

        cimCalls.Should().Be(0);
        flags.AcFitForOc.Should().Be(0);
        flags.PowerChargeMode.Should().Be(1);
        Power.ResolvePowerAdapterStatus(adapterConnected: true, acFitForOc: false, chargingNormally: true)
            .Should().Be(PowerAdapterStatus.ConnectedLowWattage);
    }

    [Fact]
    public async Task BothReadsFail_StaysUnknownAndDoesNotWarn()
    {
        var flags = await WMI.LenovoGameZoneData.ResolveAcAdapterFlagsAsync(
            () => Task.FromResult((Success: false, Value: 0)),
            () => Task.FromResult((Success: false, Value: 0)),
            () => Task.FromResult((Success: false, AcFitForOc: 0, PowerChargeMode: 0)));

        flags.AcFitForOc.Should().BeNull();
        flags.PowerChargeMode.Should().BeNull();
        Power.ResolvePowerAdapterStatus(adapterConnected: true, acFitForOc: null, chargingNormally: null)
            .Should().Be(PowerAdapterStatus.Connected);
    }

    [Fact]
    public void DisconnectedAdapter_DoesNotWarn()
    {
        Power.ResolvePowerAdapterStatus(adapterConnected: false, acFitForOc: false, chargingNormally: false)
            .Should().Be(PowerAdapterStatus.Disconnected);
    }
}
