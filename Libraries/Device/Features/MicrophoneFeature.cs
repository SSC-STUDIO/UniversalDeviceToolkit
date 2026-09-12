using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using NAudio.CoreAudioApi;

namespace UniversalDeviceToolkit.Lib.Features;

public class MicrophoneFeature : IFeature<MicrophoneState>
{
    private readonly MMDeviceEnumerator _enumerator = new();

    private IEnumerable<AudioEndpointVolume> AudioEndpointVolumes => _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Select(d => d.AudioEndpointVolume);

    public Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var isSupported = AudioEndpointVolumes.Any();
            return Task.FromResult(isSupported);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("feature-mic-supported", "Microphone support probe failed (audio endpoints).", ex);
            return Task.FromResult(false);
        }
    }

    public Task<MicrophoneState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<MicrophoneState>());
    }

    public Task<MicrophoneState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mute = AudioEndpointVolumes.Aggregate(true, (current, v) => current && v.Mute);
        var result = mute ? MicrophoneState.Off : MicrophoneState.On;
        return Task.FromResult(result);
    }

    public Task SetStateAsync(MicrophoneState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mute = MicrophoneState.Off == state;
        AudioEndpointVolumes.ForEach(v => v.Mute = mute);
        return Task.CompletedTask;
    }

    public void InvalidateResolution()
    {
    }
}
