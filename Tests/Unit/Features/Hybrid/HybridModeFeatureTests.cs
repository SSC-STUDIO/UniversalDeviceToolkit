using System;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features.Hybrid;

[Trait("Category", TestCategories.Unit)]
public class HybridModeFeatureTests : FeatureTestBase
{
    private readonly Mock<IGSyncFeature> _mockGSyncFeature;
    private readonly Mock<IIGPUModeFeature> _mockIGPUModeFeature;
    private readonly Mock<IDGPUNotify> _mockDGPUNotify;
    private readonly Mock<ICompatibilityService> _mockCompatibilityService;
    private readonly HybridModeFeature _hybridModeFeature;

    public HybridModeFeatureTests()
    {
        _mockGSyncFeature = new Mock<IGSyncFeature>();
        _mockIGPUModeFeature = new Mock<IIGPUModeFeature>();
        _mockDGPUNotify = new Mock<IDGPUNotify>();
        _mockCompatibilityService = new Mock<ICompatibilityService>();

        _hybridModeFeature = new HybridModeFeature(
            _mockGSyncFeature.Object,
            _mockIGPUModeFeature.Object,
            _mockDGPUNotify.Object,
            _mockCompatibilityService.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithMockedDependencies()
    {
        // Arrange & Act
        var feature = new HybridModeFeature(
            _mockGSyncFeature.Object,
            _mockIGPUModeFeature.Object,
            _mockDGPUNotify.Object,
            _mockCompatibilityService.Object
        );

        // Assert
        feature.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldImplementIFeatureInterface()
    {
        // Arrange & Act
        var feature = new HybridModeFeature(
            _mockGSyncFeature.Object,
            _mockIGPUModeFeature.Object,
            _mockDGPUNotify.Object,
            _mockCompatibilityService.Object
        );

        // Assert
        feature.Should().BeAssignableTo<IFeature<HybridModeState>>();
    }

    #endregion

    #region IsSupportedAsync Tests

    [Fact]
    public async Task IsSupportedAsync_WhenBothFeaturesSupported_ShouldReturnTrue()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: true, supportsIGPUMode: true);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var result = await _hybridModeFeature.IsSupportedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSupportedAsync_WhenGSyncOnlySupported_ShouldReturnTrue()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: true, supportsIGPUMode: false);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var result = await _hybridModeFeature.IsSupportedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSupportedAsync_WhenIGPUModeOnlySupported_ShouldReturnTrue()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: false, supportsIGPUMode: true);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var result = await _hybridModeFeature.IsSupportedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSupportedAsync_WhenNeitherFeatureSupported_ShouldReturnFalse()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: false, supportsIGPUMode: false);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var result = await _hybridModeFeature.IsSupportedAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetStateAsync Tests

    [Fact]
    public async Task GetStateAsync_WhenGSyncOnAndIGPUModeDefault_ShouldReturnOff()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act
        var state = await _hybridModeFeature.GetStateAsync();

        // Assert
        state.Should().Be(HybridModeState.Off);
    }

    [Fact]
    public async Task GetStateAsync_WhenGSyncOffAndIGPUModeDefault_ShouldReturnOn()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act
        var state = await _hybridModeFeature.GetStateAsync();

        // Assert
        state.Should().Be(HybridModeState.On);
    }

    [Fact]
    public async Task GetStateAsync_WhenGSyncOffAndIGPUModeIGPUOnly_ShouldReturnOnIGPUOnly()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);

        // Act
        var state = await _hybridModeFeature.GetStateAsync();

        // Assert
        state.Should().Be(HybridModeState.OnIGPUOnly);
    }

    [Fact]
    public async Task GetStateAsync_WhenGSyncOffAndIGPUModeAuto_ShouldReturnOnAuto()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Auto);

        // Act
        var state = await _hybridModeFeature.GetStateAsync();

        // Assert
        state.Should().Be(HybridModeState.OnAuto);
    }

    [Fact]
    public async Task GetStateAsync_WhenOnlyGSyncSupported_ShouldUseGSyncState()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, false, Array.Empty<IGPUModeState>(), IGPUModeState.Default);

        // Act
        var state = await _hybridModeFeature.GetStateAsync();

        // Assert
        // GSync On + IGPUMode Default (default value when not supported) = HybridMode Off
        state.Should().Be(HybridModeState.Off);
    }

    [Fact]
    public async Task GetStateAsync_WhenOnlyIGPUModeSupported_ShouldUseIGPUModeState()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, false, Array.Empty<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);

        // Act
        var state = await _hybridModeFeature.GetStateAsync();

        // Assert
        // GSync Off (default value) + IGPUMode IGPUOnly = HybridMode OnIGPUOnly
        state.Should().Be(HybridModeState.OnIGPUOnly);
    }

    #endregion

    #region SetStateAsync Tests

    [Fact]
    public async Task SetStateAsync_WhenSettingHybridModeOn_ShouldSetGSyncOffAndIGPUModeDefault()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);

        // Act
        await _hybridModeFeature.SetStateAsync(HybridModeState.On);

        // Assert
        VerifyStateChange(_mockGSyncFeature, GSyncState.Off, Times.Once());
        VerifyStateChange(_mockIGPUModeFeature, IGPUModeState.Default, Times.Once());
    }

    [Fact]
    public async Task SetStateAsync_WhenSettingHybridModeOnIGPUOnly_ShouldSetGSyncOffAndIGPUModeIGPUOnly()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act
        await _hybridModeFeature.SetStateAsync(HybridModeState.OnIGPUOnly);

        // Assert
        VerifyStateChange(_mockGSyncFeature, GSyncState.Off, Times.Once());
        VerifyStateChange(_mockIGPUModeFeature, IGPUModeState.IGPUOnly, Times.Once());
    }

    [Fact]
    public async Task SetStateAsync_WhenSettingHybridModeOnAuto_ShouldSetGSyncOffAndIGPUModeAuto()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act
        await _hybridModeFeature.SetStateAsync(HybridModeState.OnAuto);

        // Assert
        VerifyStateChange(_mockGSyncFeature, GSyncState.Off, Times.Once());
        VerifyStateChange(_mockIGPUModeFeature, IGPUModeState.Auto, Times.Once());
    }

    [Fact]
    public async Task SetStateAsync_WhenSettingHybridModeOff_ShouldSetGSyncOn()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);

        // Act
        await _hybridModeFeature.SetStateAsync(HybridModeState.Off);

        // Assert
        VerifyStateChange(_mockGSyncFeature, GSyncState.On, Times.Once());
        VerifyStateChange(_mockIGPUModeFeature, IGPUModeState.Default, Times.Once());
    }

    [Fact]
    public async Task SetStateAsync_WhenStateAlreadySet_ShouldNotCallSetState()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act - Set to On (already Off + Default)
        await _hybridModeFeature.SetStateAsync(HybridModeState.On);

        // Assert - Should not call SetStateAsync since state is already correct
        _mockGSyncFeature.Verify(f => f.SetStateAsync(It.IsAny<GSyncState>()), Times.Never());
        _mockIGPUModeFeature.Verify(f => f.SetStateAsync(It.IsAny<IGPUModeState>()), Times.Never());
    }

    [Fact]
    public async Task SetStateAsync_WhenIGPUModeNotSupported_ShouldOnlySetGSync()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, false, Array.Empty<IGPUModeState>(), IGPUModeState.Default);

        // Act
        await _hybridModeFeature.SetStateAsync(HybridModeState.On);

        // Assert
        VerifyStateChange(_mockGSyncFeature, GSyncState.Off, Times.Once());
        _mockIGPUModeFeature.Verify(f => f.SetStateAsync(It.IsAny<IGPUModeState>()), Times.Never());
    }

    [Fact]
    public async Task SetStateAsync_WhenGSyncNotSupported_ShouldOnlySetIGPUMode()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, false, Array.Empty<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act
        await _hybridModeFeature.SetStateAsync(HybridModeState.OnIGPUOnly);

        // Assert
        _mockGSyncFeature.Verify(f => f.SetStateAsync(It.IsAny<GSyncState>()), Times.Never());
        VerifyStateChange(_mockIGPUModeFeature, IGPUModeState.IGPUOnly, Times.Once());
    }

    [Fact]
    public async Task SetStateAsync_WhenIGPUModeChangeFailsWithoutGSyncChange_ShouldThrow()
    {
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);
        _mockIGPUModeFeature
            .Setup(feature => feature.SetStateAsync(IGPUModeState.Default, It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new IGPUModeChangeException(IGPUModeState.Default));

        var act = () => _hybridModeFeature.SetStateAsync(HybridModeState.On);

        await act.Should().ThrowAsync<IGPUModeChangeException>();
        _mockGSyncFeature.Verify(feature => feature.SetStateAsync(It.IsAny<GSyncState>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task SetStateAsync_WhenIGPUModeChangeFailsAfterGSyncChange_ShouldStillThrow()
    {
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);
        _mockIGPUModeFeature
            .Setup(feature => feature.SetStateAsync(IGPUModeState.Default, It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new IGPUModeChangeException(IGPUModeState.Default));

        var act = () => _hybridModeFeature.SetStateAsync(HybridModeState.On);

        await act.Should().ThrowAsync<IGPUModeChangeException>();
        VerifyStateChange(_mockGSyncFeature, GSyncState.Off, Times.Once());
    }

    [Fact]
    public async Task SetStateAsync_WhenDisposed_ShouldThrow()
    {
        _hybridModeFeature.Dispose();

        var act = () => _hybridModeFeature.SetStateAsync(HybridModeState.On);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void InvalidateResolution_ShouldInvalidateDgpuNotify()
    {
        _hybridModeFeature.InvalidateResolution();

        _mockGSyncFeature.Verify(feature => feature.InvalidateResolution(), Times.Once());
        _mockIGPUModeFeature.Verify(feature => feature.InvalidateResolution(), Times.Once());
        _mockDGPUNotify.Verify(notify => notify.InvalidateResolution(), Times.Once());
    }

    #endregion

    #region GetAllStatesAsync Tests

    [Fact]
    public async Task GetAllStatesAsync_WhenBothFeaturesSupported_ShouldReturnAllFourStates()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: true, supportsIGPUMode: true);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var states = await _hybridModeFeature.GetAllStatesAsync();

        // Assert
        states.Should().HaveCount(4);
        states.Should().Contain(HybridModeState.On);
        states.Should().Contain(HybridModeState.OnIGPUOnly);
        states.Should().Contain(HybridModeState.OnAuto);
        states.Should().Contain(HybridModeState.Off);
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenOnlyGSyncSupported_ShouldReturnOnAndOff()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: true, supportsIGPUMode: false);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var states = await _hybridModeFeature.GetAllStatesAsync();

        // Assert
        states.Should().HaveCount(2);
        states.Should().Contain(HybridModeState.On);
        states.Should().Contain(HybridModeState.Off);
    }

    [Fact]
    public async Task GetAllStatesAsync_WhenOnlyIGPUModeSupported_ShouldReturnOnIGPUOnlyOnAuto()
    {
        // Arrange
        var machineInfo = CreateMachineInformation(supportsGSync: false, supportsIGPUMode: true);
        _mockCompatibilityService
            .Setup(c => c.GetMachineInformationAsync())
            .ReturnsAsync(machineInfo);

        // Act
        var states = await _hybridModeFeature.GetAllStatesAsync();

        // Assert
        states.Should().HaveCount(3);
        states.Should().Contain(HybridModeState.On);
        states.Should().Contain(HybridModeState.OnIGPUOnly);
        states.Should().Contain(HybridModeState.OnAuto);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task HybridModeFeature_ShouldCoordinateGSyncAndIGPUModeFeatures()
    {
        // Arrange - Initial state: GSync On, IGPUMode Default => HybridMode Off
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act - Get initial state
        var initialState = await _hybridModeFeature.GetStateAsync();
        initialState.Should().Be(HybridModeState.Off);

        // Act - Set to OnIGPUOnly
        await _hybridModeFeature.SetStateAsync(HybridModeState.OnIGPUOnly);

        // Assert - Should set both features
        VerifyStateChange(_mockGSyncFeature, GSyncState.Off, Times.Once());
        VerifyStateChange(_mockIGPUModeFeature, IGPUModeState.IGPUOnly, Times.Once());

        // Arrange - Update mocks to reflect new state
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);

        // Act - Get new state
        var newState = await _hybridModeFeature.GetStateAsync();
        newState.Should().Be(HybridModeState.OnIGPUOnly);
    }

    [Fact]
    public async Task SetStateAsync_ShouldVerifyStateConsistency()
    {
        // Arrange
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        // Act - Set multiple states in sequence
        await _hybridModeFeature.SetStateAsync(HybridModeState.OnIGPUOnly);

        // Update mocks for each state
        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.Off);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.IGPUOnly);

        var state1 = await _hybridModeFeature.GetStateAsync();
        state1.Should().Be(HybridModeState.OnIGPUOnly);

        // Set to Off
        await _hybridModeFeature.SetStateAsync(HybridModeState.Off);

        SetupMockFeatureState(_mockGSyncFeature, true, Enum.GetValues<GSyncState>(), GSyncState.On);
        SetupMockFeatureState(_mockIGPUModeFeature, true, Enum.GetValues<IGPUModeState>(), IGPUModeState.Default);

        var state2 = await _hybridModeFeature.GetStateAsync();
        state2.Should().Be(HybridModeState.Off);
    }

    #endregion

    #region Helper Methods

    private static MachineInformation CreateMachineInformation(bool supportsGSync, bool supportsIGPUMode)
    {
        return new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83G0",
            Model = "16ARH8",
            SerialNumber = "TEST123",
            BiosVersion = new BiosVersion("G9CN", 30),
            BiosVersionRaw = "G9CN30WW",
            SupportedPowerModes = new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode },
            SmartFanVersion = 6,
            LegionZoneVersion = 3,
            Features = MachineInformation.FeatureData.Unknown,
            Properties = new()
            {
                SupportsGSync = supportsGSync,
                SupportsIGPUMode = supportsIGPUMode,
            }
        };
    }

    #endregion
}