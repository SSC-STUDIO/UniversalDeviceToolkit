using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;

namespace UniversalDeviceToolkit.Lib.Automation.CLI.Features;

public class FeatureRegistration<T>(string name, Func<T, string>? toStringConverter = null, Func<string, T>? fromStringConverter = null)
    : IFeatureRegistration where T : struct
{
    private static string NotSupportedMessage => GetMessage(
        "FeatureRegistration_NotSupported",
        "Feature is not supported.");
    private static string NullReturnValueMessage => GetMessage(
        "FeatureRegistration_NullReturnValue",
        "Feature returned no value.");
    private static string StateNotSupportedMessage => GetMessage(
        "FeatureRegistration_StateNotSupported",
        "State is not supported.");

    public string Name { get; } = name;

    private static string GetMessage(string key, string fallback) =>
        UniversalDeviceToolkit.Lib.Automation.Resources.Resource.ResourceManager
            .GetString(key, LocalizationRuntime.CurrentCulture)
        ?? fallback;

    private readonly Func<IFeature<T>> _feature = IoCContainer.Resolve<IFeature<T>>;

    public Task<bool> IsSupportedAsync() => _feature().IsSupportedAsync();

    public async Task<IEnumerable<string>> GetValuesAsync()
    {
        var feature = _feature();

        if (!await feature.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException(NotSupportedMessage);

        var states = await feature.GetAllStatesAsync().ConfigureAwait(false);
        return states.Select(s => toStringConverter?.Invoke(s) ?? s.ToString()?.ToLowerInvariant()).OfType<string>();
    }

    public async Task<string> GetValueAsync()
    {
        var feature = _feature();

        if (!await feature.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException(NotSupportedMessage);

        var state = await feature.GetStateAsync().ConfigureAwait(false);

        string result;

        if (toStringConverter is not null)
        {
            result = toStringConverter(state);
        }
        else
        {
            result = state.ToString()?.ToLowerInvariant() ?? throw new InvalidOperationException(NullReturnValueMessage);
        }

        return result;
    }

    public async Task SetValueAsync(string value)
    {
        var feature = _feature();

        if (!await feature.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException(NotSupportedMessage);

        var states = await feature.GetAllStatesAsync().ConfigureAwait(false);

        T state;

        if (fromStringConverter is not null)
        {
            state = fromStringConverter(value);
        }
        else
        {
            state = Enum.TryParse<T>(value, true, out var s)
                ? s
                : throw new InvalidOperationException(StateNotSupportedMessage);
        }

        if (!states.Contains(state))
            throw new InvalidOperationException(StateNotSupportedMessage);

        await feature.SetStateAsync(state).ConfigureAwait(false);
    }
}
