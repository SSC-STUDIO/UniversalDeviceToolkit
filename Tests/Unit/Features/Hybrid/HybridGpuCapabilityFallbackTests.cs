using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features.Hybrid;

[Trait("Category", TestCategories.Unit)]
public class HybridGpuCapabilityFallbackTests
{
    [Fact]
    public async Task GameZoneSupport_WhenClassicResultIsEmpty_ShouldUseCimResult()
    {
        var cimReadCount = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneSupportWithCimFallbackAsync(
            "IsSupportIGPUMode",
            () => Task.FromResult<int?>(null),
            () =>
            {
                cimReadCount++;
                return Task.FromResult<int?>(1);
            });

        result.Should().Be(1);
        cimReadCount.Should().Be(1);
    }

    [Fact]
    public async Task GameZoneSupport_WhenClassicReportsUnsupported_ShouldConfirmWithCim()
    {
        var cimReadCount = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneSupportWithCimFallbackAsync(
            "IsSupportGSync",
            () => Task.FromResult<int?>(0),
            () =>
            {
                cimReadCount++;
                return Task.FromResult<int?>(2);
            });

        result.Should().Be(2);
        cimReadCount.Should().Be(1);
    }

    [Fact]
    public async Task GameZoneSupport_WhenClassicResultIsPositive_ShouldNotInvokeCim()
    {
        var cimReadCount = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneSupportWithCimFallbackAsync(
            "IsSupportIGPUMode",
            () => Task.FromResult<int?>(1),
            () =>
            {
                cimReadCount++;
                return Task.FromResult<int?>(2);
            });

        result.Should().Be(1);
        cimReadCount.Should().Be(0);
    }

    [Fact]
    public async Task GameZoneSupport_WhenProvidersThrow_ShouldFailClosed()
    {
        var result = -1;
        Func<Task> action = async () =>
        {
            result = await WMI.LenovoGameZoneData.ResolveGameZoneSupportWithCimFallbackAsync(
                "IsSupportIGPUMode",
                () => Task.FromException<int?>(new InvalidOperationException("Classic provider failed.")),
                () => Task.FromException<int?>(new InvalidOperationException("CIM provider failed.")));
        };

        await action.Should().NotThrowAsync();
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GameZoneSupport_WhenCimDataIsMissingOrInvalid_ShouldFailClosed(int? cimValue)
    {
        var result = await WMI.LenovoGameZoneData.ResolveGameZoneSupportWithCimFallbackAsync(
            "IsSupportGSync",
            () => Task.FromResult<int?>(null),
            () => Task.FromResult(cimValue));

        result.Should().Be(0);
    }

    [Fact]
    public async Task HybridMode_When83DfCimAndCapabilityDataReportGpuModes_ShouldBeSupported()
    {
        var features = new MachineInformation.FeatureData(
            MachineInformation.FeatureData.SourceType.CapabilityData,
            [
                CapabilityID.IGPUMode,
                CapabilityID.NvidiaGPUDynamicDisplaySwitching,
            ]);
        var igpuGameZoneSupport = await ReadCimFallbackAsync("IsSupportIGPUMode", 1);
        var gSyncGameZoneSupport = await ReadCimFallbackAsync("IsSupportGSync", 1);
        var machineInformation = Create83DfMachineInformation(
            features,
            supportsIGPUMode: Compatibility.ResolveHybridGpuCapabilitySupport(
                igpuGameZoneSupport,
                features,
                CapabilityID.IGPUMode),
            supportsGSync: Compatibility.ResolveHybridGpuCapabilitySupport(
                gSyncGameZoneSupport,
                features,
                CapabilityID.NvidiaGPUDynamicDisplaySwitching));
        var feature = CreateHybridModeFeature(machineInformation);

        var supported = await feature.IsSupportedAsync();
        var states = await feature.GetAllStatesAsync();

        supported.Should().BeTrue();
        states.Should().Equal(
            HybridModeState.On,
            HybridModeState.OnIGPUOnly,
            HybridModeState.OnAuto,
            HybridModeState.Off);
    }

    [Fact]
    public async Task HybridMode_When83DfProvidersAndCapabilityDataDoNotReportGpuModes_ShouldRemainUnsupported()
    {
        var features = new MachineInformation.FeatureData(
            MachineInformation.FeatureData.SourceType.CapabilityData,
            [CapabilityID.OverDrive]);
        var igpuGameZoneSupport = await ReadCimFallbackAsync("IsSupportIGPUMode", 0);
        var gSyncGameZoneSupport = await ReadCimFallbackAsync("IsSupportGSync", 0);
        var machineInformation = Create83DfMachineInformation(
            features,
            supportsIGPUMode: Compatibility.ResolveHybridGpuCapabilitySupport(
                igpuGameZoneSupport,
                features,
                CapabilityID.IGPUMode),
            supportsGSync: Compatibility.ResolveHybridGpuCapabilitySupport(
                gSyncGameZoneSupport,
                features,
                CapabilityID.NvidiaGPUDynamicDisplaySwitching));
        var feature = CreateHybridModeFeature(machineInformation);

        var supported = await feature.IsSupportedAsync();
        var states = await feature.GetAllStatesAsync();

        supported.Should().BeFalse();
        states.Should().BeEmpty();
    }

    [Fact]
    public void HybridCapability_WhenFeatureDataIsDefaultAndProviderIsUnavailable_ShouldFailClosed()
    {
        var features = default(MachineInformation.FeatureData);

        var igpuSupported = Compatibility.ResolveHybridGpuCapabilitySupport(
            null,
            features,
            CapabilityID.IGPUMode);
        var gSyncSupported = Compatibility.ResolveHybridGpuCapabilitySupport(
            null,
            features,
            CapabilityID.NvidiaGPUDynamicDisplaySwitching);

        igpuSupported.Should().BeFalse();
        gSyncSupported.Should().BeFalse();
    }

    [Fact]
    public void HybridCapability_WhenCapabilityDataIsPartial_ShouldNotInferMissingCapabilities()
    {
        var features = new MachineInformation.FeatureData(
            MachineInformation.FeatureData.SourceType.CapabilityData,
            [CapabilityID.IGPUMode]);

        var igpuSupported = Compatibility.ResolveHybridGpuCapabilitySupport(
            null,
            features,
            CapabilityID.IGPUMode);
        var gSyncSupported = Compatibility.ResolveHybridGpuCapabilitySupport(
            null,
            features,
            CapabilityID.NvidiaGPUDynamicDisplaySwitching);

        igpuSupported.Should().BeTrue();
        gSyncSupported.Should().BeFalse();
    }

    [Fact]
    public async Task ExperimentalGpuWorkingMode_WhenEnabled_ShouldSelectCapabilityBackendWithoutGameZoneProbe()
    {
        var gameZoneFeature = new Mock<IFeature<IGPUModeState>>(MockBehavior.Strict);
        var capabilityFeature = new Mock<IFeature<IGPUModeState>>(MockBehavior.Strict);
        var featureFlagsFeature = new Mock<IFeature<IGPUModeState>>(MockBehavior.Strict);
        capabilityFeature
            .Setup(feature => feature.IsSupportedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        gameZoneFeature.Setup(feature => feature.InvalidateResolution());
        capabilityFeature.Setup(feature => feature.InvalidateResolution());
        featureFlagsFeature.Setup(feature => feature.InvalidateResolution());
        var feature = new IGPUModeFeature(
            gameZoneFeature.Object,
            capabilityFeature.Object,
            featureFlagsFeature.Object)
        {
            ExperimentalGPUWorkingMode = true,
        };

        var supported = await feature.IsSupportedAsync();

        supported.Should().BeTrue();
        feature.ExperimentalGPUWorkingMode.Should().BeTrue();
        gameZoneFeature.Verify(
            candidate => candidate.IsSupportedAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        featureFlagsFeature.Verify(
            candidate => candidate.IsSupportedAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExperimentalGpuWorkingMode_WhenToggledAfterResolve_ShouldInvalidateCachedBackend()
    {
        var gameZoneFeature = new Mock<IFeature<IGPUModeState>>(MockBehavior.Strict);
        var capabilityFeature = new Mock<IFeature<IGPUModeState>>(MockBehavior.Strict);
        var featureFlagsFeature = new Mock<IFeature<IGPUModeState>>(MockBehavior.Strict);
        gameZoneFeature
            .Setup(feature => feature.IsSupportedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        capabilityFeature
            .Setup(feature => feature.IsSupportedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        gameZoneFeature.Setup(feature => feature.InvalidateResolution());
        capabilityFeature.Setup(feature => feature.InvalidateResolution());
        featureFlagsFeature.Setup(feature => feature.InvalidateResolution());
        var feature = new IGPUModeFeature(
            gameZoneFeature.Object,
            capabilityFeature.Object,
            featureFlagsFeature.Object);

        (await feature.IsSupportedAsync()).Should().BeTrue();
        gameZoneFeature.Verify(candidate => candidate.IsSupportedAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        capabilityFeature.Verify(candidate => candidate.IsSupportedAsync(It.IsAny<CancellationToken>()), Times.Never);

        feature.ExperimentalGPUWorkingMode = true;

        (await feature.IsSupportedAsync()).Should().BeTrue();
        capabilityFeature.Verify(candidate => candidate.IsSupportedAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        gameZoneFeature.Verify(candidate => candidate.IsSupportedAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HybridMuxSupport_ShouldNotProbeOrImplyDgpuForceSleepAvailability()
    {
        var machineInformation = Create83DfMachineInformation(
            new MachineInformation.FeatureData(
                MachineInformation.FeatureData.SourceType.CapabilityData,
                [CapabilityID.IGPUMode]),
            supportsIGPUMode: true,
            supportsGSync: false);
        var compatibilityService = new Mock<ICompatibilityService>();
        compatibilityService
            .Setup(service => service.GetMachineInformationAsync())
            .ReturnsAsync(machineInformation);
        var dgpuNotify = new Mock<IDGPUNotify>(MockBehavior.Strict);
        var feature = new HybridModeFeature(
            Mock.Of<IGSyncFeature>(),
            Mock.Of<IIGPUModeFeature>(),
            dgpuNotify.Object,
            compatibilityService.Object);

        var supported = await feature.IsSupportedAsync();

        supported.Should().BeTrue();
        dgpuNotify.VerifyNoOtherCalls();
    }

    private static Task<int> ReadCimFallbackAsync(string methodName, int cimValue) =>
        WMI.LenovoGameZoneData.ResolveGameZoneSupportWithCimFallbackAsync(
            methodName,
            () => Task.FromResult<int?>(null),
            () => Task.FromResult<int?>(cimValue));

    private static HybridModeFeature CreateHybridModeFeature(MachineInformation machineInformation)
    {
        var compatibilityService = new Mock<ICompatibilityService>();
        compatibilityService
            .Setup(service => service.GetMachineInformationAsync())
            .ReturnsAsync(machineInformation);

        return new HybridModeFeature(
            Mock.Of<IGSyncFeature>(),
            Mock.Of<IIGPUModeFeature>(),
            Mock.Of<IDGPUNotify>(),
            compatibilityService.Object);
    }

    private static MachineInformation Create83DfMachineInformation(
        MachineInformation.FeatureData features,
        bool supportsIGPUMode,
        bool supportsGSync) =>
        new()
        {
            Vendor = "LENOVO",
            MachineType = "83DF",
            Model = "Legion Y9000P IRX9",
            SerialNumber = "TEST-83DF",
            SupportedPowerModes = [],
            Features = features,
            Properties = new MachineInformation.PropertyData
            {
                SupportsIGPUMode = supportsIGPUMode,
                SupportsGSync = supportsGSync,
            },
        };
}
