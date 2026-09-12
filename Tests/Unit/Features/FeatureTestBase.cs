using Moq;
using UniversalDeviceToolkit.Lib.Features;

namespace UniversalDeviceToolkit.Tests.Features;

/// <summary>
/// Base class for Feature tests providing common test infrastructure
/// </summary>
public abstract class FeatureTestBase
{
    /// <summary>
    /// Setup a mock feature to return specific state
    /// </summary>
    protected void SetupMockFeatureState<TFeature, TState>(
        Mock<TFeature> mock,
        bool isSupported,
        TState[] allStates,
        TState currentState) where TFeature : class, IFeature<TState> where TState : struct
    {
        mock
            .Setup(f => f.IsSupportedAsync())
            .ReturnsAsync(isSupported);

        mock
            .Setup(f => f.GetAllStatesAsync())
            .ReturnsAsync(allStates);

        mock
            .Setup(f => f.GetStateAsync())
            .ReturnsAsync(currentState);
    }

    /// <summary>
    /// Verify feature state change was called
    /// </summary>
    protected void VerifyStateChange<TFeature, TState>(
        Mock<TFeature> mock,
        TState expectedState,
        Times times) where TFeature : class, IFeature<TState> where TState : struct
    {
        mock.Verify(
            f => f.SetStateAsync(expectedState),
            times);
    }
}
