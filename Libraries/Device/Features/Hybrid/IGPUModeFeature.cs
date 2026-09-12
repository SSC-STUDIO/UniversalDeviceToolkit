using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

public class IGPUModeFeature : AbstractCompositeFeature<IGPUModeState>, IIGPUModeFeature
{
    private readonly IFeature<IGPUModeState> _gameZoneFeature;
    private readonly IFeature<IGPUModeState> _capabilityFeature;
    private readonly IFeature<IGPUModeState> _featureFlagsFeature;

    public IGPUModeFeature(
        IGPUModeGamezoneFeature gameZoneFeature,
        IGPUModeCapabilityFeature capabilityFeature,
        IGPUModeFeatureFlagsFeature featureFlagsFeature)
        : this(
            (IFeature<IGPUModeState>)gameZoneFeature,
            capabilityFeature,
            featureFlagsFeature)
    {
    }

    internal IGPUModeFeature(
        IFeature<IGPUModeState> gameZoneFeature,
        IFeature<IGPUModeState> capabilityFeature,
        IFeature<IGPUModeState> featureFlagsFeature)
        : base(gameZoneFeature, capabilityFeature, featureFlagsFeature)
    {
        _gameZoneFeature = gameZoneFeature;
        _capabilityFeature = capabilityFeature;
        _featureFlagsFeature = featureFlagsFeature;
    }

    private bool _experimentalGPUWorkingMode;

    public bool ExperimentalGPUWorkingMode
    {
        get => _experimentalGPUWorkingMode;
        set
        {
            if (_experimentalGPUWorkingMode == value)
                return;

            _experimentalGPUWorkingMode = value;
            InvalidateResolution();
        }
    }

    protected override async Task<IFeature<IGPUModeState>?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (ExperimentalGPUWorkingMode)
        {
            if (await _capabilityFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
                return _capabilityFeature;

            if (await _featureFlagsFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
                return _featureFlagsFeature;

            return null;
        }

        if (await _gameZoneFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            return _gameZoneFeature;

        if (await _capabilityFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            return _capabilityFeature;

        if (await _featureFlagsFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            return _featureFlagsFeature;

        return null;
    }
}
