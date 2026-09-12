using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using NeoSmart.AsyncLock;
using Windows.Win32;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers;

public class SpectrumKeyboardBacklightController : IDisposable
{
    public interface ISpectrumScreenCapture
    {
        void CaptureScreen(ref RGBColor[,] buffer, int width, int height, CancellationToken token);
    }

    private readonly struct KeyMap(int width, int height, ushort[,] keyCodes, ushort[] additionalKeyCodes)
    {
        public static readonly KeyMap Empty = new(0, 0, new ushort[0, 0], []);

        public readonly int Width = width;
        public readonly int Height = height;
        public readonly ushort[,] KeyCodes = keyCodes;
        public readonly ushort[] AdditionalKeyCodes = additionalKeyCodes;
    }

    private static readonly AsyncLock GetDeviceHandleLock = new();
    private static readonly SemaphoreSlim IoSemaphore = new(1, 1); // Use SemaphoreSlim for better async performance than lock

    private readonly TimeSpan _auroraRefreshInterval = TimeSpan.FromMilliseconds(60);

    private readonly SpecialKeyListener _listener;
    private readonly VantageDisabler _vantageDisabler;
    private readonly ISpectrumScreenCapture _screenCapture;
    private readonly IDelayProvider _delayProvider;

    private SafeFileHandle? _deviceHandle;

    private readonly AsyncLock _auroraLock = new();
    private CancellationTokenSource? _auroraRefreshCancellationTokenSource;
    private Task? _auroraRefreshTask;
    private bool _auroraCaptureFailureLogged;
    private readonly List<LENOVO_SPECTRUM_AURORA_ITEM> _auroraItemsBuffer = new();

    private static readonly JsonSerializerOptions SpectrumProfileJsonOptions = LltJson.CreateSettingsOptions();

    public bool ForceDisable { get; set; }

    public SpectrumKeyboardBacklightController(SpecialKeyListener listener, VantageDisabler vantageDisabler, ISpectrumScreenCapture screenCapture, IDelayProvider delayProvider)
    {
        _listener = listener;
        _vantageDisabler = vantageDisabler;
        _screenCapture = screenCapture;
        _delayProvider = delayProvider;

        _listener.Changed += Listener_Changed;
    }

private async Task Listener_ChangedAsync(object? sender, SpecialKeyListener.ChangedEventArgs e)
    {
        try
        {
            if (!await IsSupportedAsync().ConfigureAwait(false))
                return;

            if (await _vantageDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
                return;

            switch (e.SpecialKey)
            {
                case SpecialKey.SpectrumPreset1
                    or SpecialKey.SpectrumPreset2
                    or SpecialKey.SpectrumPreset3
                    or SpecialKey.SpectrumPreset4
                    or SpecialKey.SpectrumPreset5
                    or SpecialKey.SpectrumPreset6:
                    {
                        // LED / aurora feedback must not block the key event path.
                        await Listeners.SpecialKeyLedIsolation.RunLedFeedbackAsync(
                            $"spectrum-preset-{e.SpecialKey}",
                            () => StartAuroraIfNeededAsync()).ConfigureAwait(false);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in Listener_Changed: {ex.Message}", ex);
        }
    }

    // Event handler wrapper that properly handles async task
    private void Listener_Changed(object? sender, SpecialKeyListener.ChangedEventArgs e)
    {
        _ = Listener_ChangedAsync(sender, e);
    }

    public async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (!Compatibility.IsSupportedLegionMachine(mi))
            return false;

        return await GetDeviceHandleAsync().ConfigureAwait(false) is not null;
    }

    public async Task<(SpectrumLayout, KeyboardLayout, HashSet<ushort>)> GetKeyboardLayoutAsync()
    {
        var (width, height, keys) = await ReadAllKeyCodesAsync().ConfigureAwait(false);
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        var spectrumLayout = (width, height) switch
        {
            (22, 9) when mi.Properties.HasAlternativeFullSpectrumLayout => SpectrumLayout.FullAlternative,
            (22, 9) => SpectrumLayout.Full,
            (20, 8) => SpectrumLayout.KeyboardAndFront,
            _ => SpectrumLayout.KeyboardOnly // (20, 7)
        };

        KeyboardLayout keyboardLayout;
        if (keys.Contains(0xA9))
            keyboardLayout = KeyboardLayout.Jis;
        else if (keys.Contains(0xA8))
            keyboardLayout = KeyboardLayout.Iso;
        else
            keyboardLayout = KeyboardLayout.Ansi;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Keyboard layout: {spectrumLayout}, {keyboardLayout}");

        return (spectrumLayout, keyboardLayout, keys);
    }

    public async Task<int> GetBrightnessAsync()
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        var input = new LENOVO_SPECTRUM_GET_BRIGHTNESS_REQUEST();
        var output = await SetAndGetFeature<LENOVO_SPECTRUM_GET_BRIGHTNESS_REQUEST, LENOVO_SPECTRUM_GET_BRIGHTNESS_RESPONSE>(handle, input).ConfigureAwait(false);
        var result = output.Brightness;

        return result;
    }

    public async Task SetBrightnessAsync(int brightness)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        if (brightness is < 0 or > 9)
            throw ExceptionHelper.BrightnessRange();

        var input = new LENOVO_SPECTRUM_SET_BRIGHTNESS_REQUEST((byte)brightness);
        await SetFeatureAsync(handle, input).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Keyboard brightness set to {brightness}");
    }

    public async Task<bool> GetLogoStatusAsync()
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        var input = new LENOVO_SPECTRUM_GET_LOGO_STATUS();
        var output = await SetAndGetFeature<LENOVO_SPECTRUM_GET_LOGO_STATUS, LENOVO_SPECTRUM_GET_LOGO_STATUS_RESPONSE>(handle, input).ConfigureAwait(false);
        var result = output.IsOn;

        return result;
    }

    public async Task SetLogoStatusAsync(bool isOn)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        var input = new LENOVO_SPECTRUM_SET_LOGO_STATUS_REQUEST(isOn);
        await SetFeatureAsync(handle, input).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Logo status set to {isOn}");
    }

    public async Task<int> GetProfileAsync()
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        var input = new LENOVO_SPECTRUM_GET_PROFILE_REQUEST();
        var output = await SetAndGetFeature<LENOVO_SPECTRUM_GET_PROFILE_REQUEST, LENOVO_SPECTRUM_GET_PROFILE_RESPONSE>(handle, input).ConfigureAwait(false);
        var result = output.Profile;

        return result;
    }

    public async Task SetProfileAsync(int profile)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        await StopAuroraIfNeededAsync().ConfigureAwait(false);

        if (profile is < 0 or > 6)
            throw ExceptionHelper.ProfileRange();

        var input = new LENOVO_SPECTRUM_SET_PROFILE_REQUEST((byte)profile);
        await SetFeatureAsync(handle, input).ConfigureAwait(false);

        await _delayProvider.Delay(TimeSpan.FromMilliseconds(100), default).ConfigureAwait(false);

        await StartAuroraIfNeededAsync(profile).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Keyboard profile set to {profile}");
    }

    public async Task SetProfileDefaultAsync(int profile)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        var input = new LENOVO_SPECTRUM_SET_PROFILE_DEFAULT_REQUEST((byte)profile);
        await SetFeatureAsync(handle, input).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Keyboard profile {profile} reset to default");
    }

    public async Task SetProfileDescriptionAsync(int profile, SpectrumKeyboardBacklightEffect[] effects)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        effects = Compress(effects);
        var bytes = Convert(profile, effects).ToBytes();
        await SetFeatureAsync(handle, bytes).ConfigureAwait(false);

        await StartAuroraIfNeededAsync(profile).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Profile {profile} set with {effects.Length} effects");
    }

    public async Task<(int Profile, SpectrumKeyboardBacklightEffect[] Effects)> GetProfileDescriptionAsync(int profile)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw new InvalidOperationException(nameof(handle));

        var input = new LENOVO_SPECTRUM_GET_EFFECT_REQUEST((byte)profile);
        var buffer = await SetAndGetFeature<LENOVO_SPECTRUM_GET_EFFECT_REQUEST>(handle, input, 960).ConfigureAwait(false);

        var description = LENOVO_SPECTRUM_EFFECT_DESCRIPTION.FromBytes(buffer);
        var result = Convert(description);

        return result;
    }

    public async Task ImportProfileDescription(int profile, string jsonPath)
    {
        var json = await File.ReadAllTextAsync(jsonPath).ConfigureAwait(false);
        var effects = JsonSerializer.Deserialize<SpectrumKeyboardBacklightEffect[]>(json, SpectrumProfileJsonOptions)
                      ?? throw ExceptionHelper.CouldNotDeserializeEffects();

        await SetProfileDescriptionAsync(profile, effects).ConfigureAwait(false);
    }

    public async Task ExportProfileDescriptionAsync(int profile, string jsonPath)
    {
        var (_, effects) = await GetProfileDescriptionAsync(profile).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(effects, SpectrumProfileJsonOptions);
        await File.WriteAllTextAsync(jsonPath, json).ConfigureAwait(false);
    }

    public async Task<bool> StartAuroraIfNeededAsync(int? profile = null)
    {
        await ThrowIfVantageEnabled().ConfigureAwait(false);

        using (await _auroraLock.LockAsync().ConfigureAwait(false))
        {
            await StopAuroraCoreAsync().ConfigureAwait(false);

            profile ??= await GetProfileAsync().ConfigureAwait(false);
            var (_, effects) = await GetProfileDescriptionAsync(profile.Value).ConfigureAwait(false);

            if (!effects.Any(e => e.Type == SpectrumKeyboardBacklightEffectType.AuroraSync))
                return false;

            _auroraRefreshCancellationTokenSource = new();
            var token = _auroraRefreshCancellationTokenSource.Token;
            _auroraRefreshTask = Task.Run(() => AuroraRefreshAsync(profile.Value, token), token);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Aurora started (profile={profile})");

            return true;
        }
    }

    public async Task StopAuroraIfNeededAsync()
    {
        try
        {
            await ThrowIfVantageEnabled().ConfigureAwait(false);

            using (await _auroraLock.LockAsync().ConfigureAwait(false))
                await StopAuroraCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error stopping Aurora: {ex.Message}");
        }
    }

    private async Task StopAuroraCoreAsync()
    {
        var sw = global::System.Diagnostics.Stopwatch.StartNew();

        if (_auroraRefreshCancellationTokenSource is not null)
            await _auroraRefreshCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        var orphanedTask = _auroraRefreshTask;
        _auroraRefreshTask = null;

        if (_auroraRefreshCancellationTokenSource is not null)
        {
            _auroraRefreshCancellationTokenSource.Dispose();
            _auroraRefreshCancellationTokenSource = null;
        }

        if (orphanedTask is not null)
        {
            try
            {
                await orphanedTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Aurora task did not complete within 5 seconds, abandoning.");
            }
            catch (OperationCanceledException)
            {
                // Expected when task is cancelled
            }
        }

        sw.Stop();
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Aurora stopped in {sw.ElapsedMilliseconds}ms.");
    }

    public async Task<Dictionary<ushort, RGBColor>> GetStateAsync(bool skipVantageCheck = false)
    {
        if (!skipVantageCheck)
            await ThrowIfVantageEnabled().ConfigureAwait(false);

        var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
        if (handle is null)
            throw ExceptionHelper.DeviceHandleNotAvailable();

        var state = await GetFeatureAsync<LENOVO_SPECTRUM_STATE_RESPONSE>(handle).ConfigureAwait(false);

        var dict = new Dictionary<ushort, RGBColor>();

        foreach (var key in state.Data.Where(k => k.KeyCode > 0))
        {
            var rgb = new RGBColor(key.Color.R, key.Color.G, key.Color.B);
            dict.TryAdd(key.KeyCode, rgb);
        }

        return dict;
    }

    private async Task ThrowIfVantageEnabled()
    {
        var vantageStatus = await _vantageDisabler.GetStatusAsync().ConfigureAwait(false);
        if (vantageStatus == SoftwareStatus.Enabled)
            throw ExceptionHelper.CantManageWithVantage();
    }

    private async Task<(int Width, int Height, HashSet<ushort> Keys)> ReadAllKeyCodesAsync()
    {
        var keyMap = await GetKeyMapAsync().ConfigureAwait(false);
        var keyCodes = new HashSet<ushort>(keyMap.Width * keyMap.Height);

        foreach (var keyCode in keyMap.KeyCodes)
            if (keyCode > 0)
                keyCodes.Add(keyCode);

        foreach (var keyCode in keyMap.AdditionalKeyCodes)
            if (keyCode > 0)
                keyCodes.Add(keyCode);

        return (keyMap.Width, keyMap.Height, keyCodes);
    }

    private async Task<KeyMap> GetKeyMapAsync()
    {
        try
        {
            var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
            if (handle is null)
                return KeyMap.Empty;

            var keyCountResponse = await SetAndGetFeature<LENOVO_SPECTRUM_GET_KEY_COUNT_REQUEST, LENOVO_SPECTRUM_GET_KEY_COUNT_RESPONSE>(
                handle,
                new LENOVO_SPECTRUM_GET_KEY_COUNT_REQUEST()).ConfigureAwait(false);

            var width = keyCountResponse.KeysPerIndex;
            var height = keyCountResponse.Indexes;

            var keyCodes = new ushort[width, height];
            var additionalKeyCodes = new ushort[width];

            for (var y = 0; y < height; y++)
            {
                var keyPageResponse = await SetAndGetFeature<LENOVO_SPECTRUM_GET_KEY_PAGE_REQUEST, LENOVO_SPECTRUM_GET_KEY_PAGE_RESPONSE>(
                    handle,
                    new LENOVO_SPECTRUM_GET_KEY_PAGE_REQUEST((byte)y)).ConfigureAwait(false);

                for (var x = 0; x < width; x++)
                    keyCodes[x, y] = keyPageResponse.Items[x].KeyCode;
            }

            var secondaryKeyPageResponse = await SetAndGetFeature<LENOVO_SPECTRUM_GET_KEY_PAGE_REQUEST, LENOVO_SPECTRUM_GET_KEY_PAGE_RESPONSE>(
                handle,
                new LENOVO_SPECTRUM_GET_KEY_PAGE_REQUEST(0, true)).ConfigureAwait(false);

            for (var x = 0; x < width; x++)
                additionalKeyCodes[x] = secondaryKeyPageResponse.Items[x].KeyCode;

            return new(width, height, keyCodes, additionalKeyCodes);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "spectrum-keymap",
                "Failed to read Spectrum keyboard key map.",
                ex);
            return KeyMap.Empty;
        }
    }

    private async Task AuroraRefreshAsync(int profile, CancellationToken token)
    {
        try
        {
            await ThrowIfVantageEnabled().ConfigureAwait(false);

            var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
            if (handle is null)
                throw ExceptionHelper.DeviceHandleNotAvailable();

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Aurora refresh starting...");

            var keyMap = await GetKeyMapAsync().ConfigureAwait(false);
            var width = keyMap.Width;
            var height = keyMap.Height;
            var colorBuffer = new RGBColor[width, height];

            await SetFeatureAsync(handle, new LENOVO_SPECTRUM_AURORA_START_STOP_REQUEST(true, (byte)profile)).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                // Overlap delay — intentional. The delay runs concurrently with the
                // screen-capture + send work below so the next refresh interval
                // starts measuring from when this loop iteration began, not when it ends.
                var delay = _delayProvider.Delay(_auroraRefreshInterval, token);

                try
                {
                    _screenCapture.CaptureScreen(ref colorBuffer, width, height, token);
                    _auroraCaptureFailureLogged = false;
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled && !_auroraCaptureFailureLogged)
                    {
                        Log.Instance.Trace($"Screen capture failed. Delaying before next refresh...", ex);
                        _auroraCaptureFailureLogged = true;
                    }

                    await _delayProvider.Delay(TimeSpan.FromMilliseconds(1000), token).ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();

                _auroraItemsBuffer.Clear();
                if (_auroraItemsBuffer.Capacity < width * height)
                    _auroraItemsBuffer.Capacity = width * height;
                var items = _auroraItemsBuffer;

                var avgR = 0;
                var avgG = 0;
                var avgB = 0;

                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        var keyCode = keyMap.KeyCodes[x, y];
                        if (keyCode < 1)
                            continue;

                        var color = colorBuffer[x, y];
                        avgR += color.R;
                        avgG += color.G;
                        avgB += color.B;
                        items.Add(new(keyCode, new(color.R, color.G, color.B)));
                    }
                }

                if (items.Count > 0)
                {
                    avgR /= items.Count;
                    avgG /= items.Count;
                    avgB /= items.Count;
                }

                for (var x = 0; x < width; x++)
                {
                    var keyCode = keyMap.AdditionalKeyCodes[x];
                    if (keyCode < 1)
                        continue;

                    // RGB order must match main key path: (R, G, B) — not (R, B, G).
                    items.Add(new(keyCode, new((byte)avgR, (byte)avgG, (byte)avgB)));
                }

                token.ThrowIfCancellationRequested();

                await SetFeatureAsync(handle, new LENOVO_SPECTRUM_AURORA_SEND_BITMAP_REQUEST([.. items]).ToBytes()).ConfigureAwait(false);

                await delay.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when Aurora refresh is cancelled, no action needed
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unexpected exception while refreshing Aurora.", ex);
        }
        finally
        {
            var handle = await GetDeviceHandleAsync().ConfigureAwait(false);
            if (handle is not null)
            {
                var currentProfile = await GetProfileAsync().ConfigureAwait(false);
                await SetFeatureAsync(handle, new LENOVO_SPECTRUM_AURORA_START_STOP_REQUEST(false, (byte)currentProfile)).ConfigureAwait(false);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Aurora refresh stopped.");
        }
    }

    private async Task<SafeFileHandle?> GetDeviceHandleAsync()
    {
        if (ForceDisable)
            return null;

        try
        {
            using (await GetDeviceHandleLock.LockAsync().ConfigureAwait(false))
            {
                if (_deviceHandle is not null && await IsReadyAsync(_deviceHandle).ConfigureAwait(false))
                    return _deviceHandle;
 
                SafeFileHandle? newDeviceHandle = null;

                const int RETRIES = 3;
                const int DELAY = 10;

                for (var i = 0; i < RETRIES; i++)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Refreshing handle... [retry={i + 1}]");

                    var tempDeviceHandle = Devices.GetSpectrumRGBKeyboard(true);
                    if (tempDeviceHandle is null)
                    {
                        if (i < RETRIES - 1)
                            await _delayProvider.Delay(TimeSpan.FromMilliseconds(DELAY), default).ConfigureAwait(false);
                        continue;
                    }

                    if (await IsReadyAsync(tempDeviceHandle).ConfigureAwait(false))
                    {
                        newDeviceHandle = tempDeviceHandle;
                        break;
                    }

                    // Not ready — dispose before next retry (forceRefresh opens a new handle each time).
                    tempDeviceHandle.Dispose();

                    if (i < RETRIES - 1)
                        await _delayProvider.Delay(TimeSpan.FromMilliseconds(DELAY), default).ConfigureAwait(false);
                }

                if (newDeviceHandle is null)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Handle couldn't be refreshed.");

                    return null;
                }

                var res = await SetAndGetFeature<LENOVO_SPECTRUM_GET_COMPATIBILITY_REQUEST, LENOVO_SPECTRUM_GET_COMPATIBILITY_RESPONSE>(
                    newDeviceHandle,
                    new LENOVO_SPECTRUM_GET_COMPATIBILITY_REQUEST()).ConfigureAwait(false);

                if (!res.IsCompatible)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Handle not compatible.");

                    newDeviceHandle.Dispose();
                    return null;
                }

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Handle refreshed.");

                _deviceHandle?.Dispose();
                _deviceHandle = newDeviceHandle;
                return newDeviceHandle;
            }
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "spectrum-device-handle",
                "Failed to open/refresh Spectrum keyboard device handle.",
                ex);
            return null;
        }
    }

    private static async Task<bool> IsReadyAsync(SafeHandle handle)
    {
        try
        {
            var b = new byte[960];
            b[0] = 7;
            await SetFeatureAsync(handle, b).ConfigureAwait(false);
            return true;
        }
        catch
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Keyboard not ready.");

            return false;
        }
    }

    private static async Task<TOut> SetAndGetFeature<TIn, TOut>(SafeHandle handle, TIn input, CancellationToken cancellationToken = default) where TIn : notnull where TOut : struct
    {
        await IoSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetFeatureInternalAsync(handle, input).ConfigureAwait(false);
            return await GetFeatureInternalAsync<TOut>(handle).ConfigureAwait(false);
        }
        finally
        {
            IoSemaphore.Release();
        }
    }

    private static async Task<byte[]> SetAndGetFeature<TIn>(SafeHandle handle, TIn input, int size, CancellationToken cancellationToken = default) where TIn : notnull
    {
        await IoSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetFeatureInternalAsync(handle, input).ConfigureAwait(false);
            return await GetFeatureInternalAsync(handle, size).ConfigureAwait(false);
        }
        finally
        {
            IoSemaphore.Release();
        }
    }

    private static async Task SetFeatureAsync<T>(SafeHandle handle, T str, CancellationToken cancellationToken = default) where T : notnull
    {
        await IoSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SetFeatureInternalAsync(handle, str).ConfigureAwait(false);
        }
        finally
        {
            IoSemaphore.Release();
        }
    }

    private static Task SetFeatureInternalAsync<T>(SafeHandle handle, T str) where T : notnull
    {
        // Execute synchronously to ensure atomicity with semaphore protection
        // Using Task.Run() could allow interleaving of unsafe operations at the thread pool level
        // Since we're already protected by IoSemaphore, we can execute directly
        unsafe
        {
            var ptr = IntPtr.Zero;
            try
            {
                int size;
                if (str is byte[] bytes)
                {
                    size = bytes.Length;
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.Copy(bytes, 0, ptr, size);
                }
                else
                {
                    size = Marshal.SizeOf<T>();
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(str, ptr, false);
                }

                var result = PInvoke.HidD_SetFeature(handle.ToWin32Handle(), ptr.ToPointer(), (uint)size);
                if (!result)
                    PInvokeExtensions.ThrowIfWin32Error(typeof(T).Name);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }
        return Task.CompletedTask;
    }

    private static async Task<T> GetFeatureAsync<T>(SafeHandle handle, CancellationToken cancellationToken = default) where T : struct
    {
        await IoSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetFeatureInternalAsync<T>(handle).ConfigureAwait(false);
        }
        finally
        {
            IoSemaphore.Release();
        }
    }

    private static Task<T> GetFeatureInternalAsync<T>(SafeHandle handle) where T : struct
    {
        // Execute synchronously to ensure atomicity with semaphore protection
        // Using Task.Run() could allow interleaving of unsafe operations at the thread pool level
        // Since we're already protected by IoSemaphore, we can execute directly
        unsafe
        {
            var ptr = IntPtr.Zero;
            try
            {
                var size = Marshal.SizeOf<T>();
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(new byte[] { 7 }, 0, ptr, 1);

                var result = PInvoke.HidD_GetFeature(handle.ToWin32Handle(), ptr.ToPointer(), (uint)size);
                if (!result)
                    PInvokeExtensions.ThrowIfWin32Error(typeof(T).Name);

                return Task.FromResult(Marshal.PtrToStructure<T>(ptr));
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }
    }

    private static async Task<byte[]> GetFeatureAsync(SafeHandle handle, int size, CancellationToken cancellationToken = default)
    {
        await IoSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetFeatureInternalAsync(handle, size).ConfigureAwait(false);
        }
        finally
        {
            IoSemaphore.Release();
        }
    }

    private static Task<byte[]> GetFeatureInternalAsync(SafeHandle handle, int size)
    {
        // Execute synchronously to ensure atomicity with semaphore protection
        // Using Task.Run() could allow interleaving of unsafe operations at the thread pool level
        // Since we're already protected by IoSemaphore, we can execute directly
        unsafe
        {
            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(new byte[] { 7 }, 0, ptr, 1);

                var result = PInvoke.HidD_GetFeature(handle.ToWin32Handle(), ptr.ToPointer(), (uint)size);
                if (!result)
                    PInvokeExtensions.ThrowIfWin32Error("bytes");

                var resultBytes = new byte[size];
                Marshal.Copy(ptr, resultBytes, 0, size);
                return Task.FromResult(resultBytes);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }
    }

    // Removed SetFeature and GetFeature synchronous wrappers to prevent deadlocks.
    // All callers should use SetFeatureAsync and GetFeatureAsync directly.

    private static SpectrumKeyboardBacklightEffect[] Compress(SpectrumKeyboardBacklightEffect[] effects)
    {
        if (effects.Any(e => e.Type.IsAllLightsEffect()))
            return [effects.Last(e => e.Type.IsAllLightsEffect())];

        var usedKeyCodes = new HashSet<ushort>();
        var newEffects = new List<SpectrumKeyboardBacklightEffect>();

        foreach (var effect in effects.Reverse())
        {
            if (effect.Type.IsWholeKeyboardEffect() && usedKeyCodes.Intersect(effect.Keys).Any())
                continue;

            var newKeyCodes = effect.Keys.Except(usedKeyCodes).ToArray();

            foreach (var keyCode in newKeyCodes)
                usedKeyCodes.Add(keyCode);

            if (newKeyCodes.IsEmpty())
                continue;

            var newEffect = new SpectrumKeyboardBacklightEffect(effect.Type,
                effect.Speed,
                effect.Direction,
                effect.ClockwiseDirection,
                effect.Colors,
                newKeyCodes);

            newEffects.Add(newEffect);
        }

        newEffects.Reverse();
        return [.. newEffects];
    }

    private static (int Profile, SpectrumKeyboardBacklightEffect[] Effects) Convert(LENOVO_SPECTRUM_EFFECT_DESCRIPTION description)
    {
        var profile = description.Profile;
        var effects = description.Effects.Select(Convert).ToArray();
        return (profile, effects);
    }

    private static SpectrumKeyboardBacklightEffect Convert(LENOVO_SPECTRUM_EFFECT effect)
    {
        var effectType = effect.EffectHeader.EffectType switch
        {
            LENOVO_SPECTRUM_EFFECT_TYPE.Always => SpectrumKeyboardBacklightEffectType.Always,
            LENOVO_SPECTRUM_EFFECT_TYPE.LegionAuraSync => SpectrumKeyboardBacklightEffectType.AuroraSync,
            LENOVO_SPECTRUM_EFFECT_TYPE.AudioBounceLighting => SpectrumKeyboardBacklightEffectType.AudioBounce,
            LENOVO_SPECTRUM_EFFECT_TYPE.AudioRippleLighting => SpectrumKeyboardBacklightEffectType.AudioRipple,
            LENOVO_SPECTRUM_EFFECT_TYPE.ColorChange => SpectrumKeyboardBacklightEffectType.ColorChange,
            LENOVO_SPECTRUM_EFFECT_TYPE.ColorPulse => SpectrumKeyboardBacklightEffectType.ColorPulse,
            LENOVO_SPECTRUM_EFFECT_TYPE.ColorWave => SpectrumKeyboardBacklightEffectType.ColorWave,
            LENOVO_SPECTRUM_EFFECT_TYPE.Rain => SpectrumKeyboardBacklightEffectType.Rain,
            LENOVO_SPECTRUM_EFFECT_TYPE.ScrewRainbow => SpectrumKeyboardBacklightEffectType.RainbowScrew,
            LENOVO_SPECTRUM_EFFECT_TYPE.RainbowWave => SpectrumKeyboardBacklightEffectType.RainbowWave,
            LENOVO_SPECTRUM_EFFECT_TYPE.Ripple => SpectrumKeyboardBacklightEffectType.Ripple,
            LENOVO_SPECTRUM_EFFECT_TYPE.Smooth => SpectrumKeyboardBacklightEffectType.Smooth,
            LENOVO_SPECTRUM_EFFECT_TYPE.TypeLighting => SpectrumKeyboardBacklightEffectType.Type,
            _ => throw new ArgumentException(nameof(effect.EffectHeader.EffectType))
        };

        var speed = effect.EffectHeader.Speed switch
        {
            LENOVO_SPECTRUM_SPEED.Speed1 => SpectrumKeyboardBacklightSpeed.Speed1,
            LENOVO_SPECTRUM_SPEED.Speed2 => SpectrumKeyboardBacklightSpeed.Speed2,
            LENOVO_SPECTRUM_SPEED.Speed3 => SpectrumKeyboardBacklightSpeed.Speed3,
            _ => SpectrumKeyboardBacklightSpeed.None
        };

        var direction = effect.EffectHeader.Direction switch
        {
            LENOVO_SPECTRUM_DIRECTION.LeftToRight => SpectrumKeyboardBacklightDirection.LeftToRight,
            LENOVO_SPECTRUM_DIRECTION.RightToLeft => SpectrumKeyboardBacklightDirection.RightToLeft,
            LENOVO_SPECTRUM_DIRECTION.BottomToTop => SpectrumKeyboardBacklightDirection.BottomToTop,
            LENOVO_SPECTRUM_DIRECTION.TopToBottom => SpectrumKeyboardBacklightDirection.TopToBottom,
            _ => SpectrumKeyboardBacklightDirection.None
        };

        var clockwiseDirection = effect.EffectHeader.ClockwiseDirection switch
        {
            LENOVO_SPECTRUM_CLOCKWISE_DIRECTION.Clockwise => SpectrumKeyboardBacklightClockwiseDirection.Clockwise,
            LENOVO_SPECTRUM_CLOCKWISE_DIRECTION.CounterClockwise => SpectrumKeyboardBacklightClockwiseDirection.CounterClockwise,
            _ => SpectrumKeyboardBacklightClockwiseDirection.None
        };

        var colors = effect.Colors.Select(c => new RGBColor(c.R, c.G, c.B)).ToArray();

        var keys = effect.KeyCodes;
        if (effect.KeyCodes is [0x65])
            keys = [];

        return new(effectType, speed, direction, clockwiseDirection, colors, keys);
    }

    private static LENOVO_SPECTRUM_EFFECT_DESCRIPTION Convert(int profile, SpectrumKeyboardBacklightEffect[] effects)
    {
        var header = new LENOVO_SPECTRUM_HEADER(LENOVO_SPECTRUM_OPERATION_TYPE.EffectChange, 0); // Size will be set on serialization
        var str = effects.Select((e, i) => Convert(i, e)).ToArray();
        var result = new LENOVO_SPECTRUM_EFFECT_DESCRIPTION(header, (byte)profile, str);
        return result;
    }

    private static LENOVO_SPECTRUM_EFFECT Convert(int index, SpectrumKeyboardBacklightEffect effect)
    {
        var effectType = effect.Type switch
        {
            SpectrumKeyboardBacklightEffectType.Always => LENOVO_SPECTRUM_EFFECT_TYPE.Always,
            SpectrumKeyboardBacklightEffectType.AuroraSync => LENOVO_SPECTRUM_EFFECT_TYPE.LegionAuraSync,
            SpectrumKeyboardBacklightEffectType.AudioBounce => LENOVO_SPECTRUM_EFFECT_TYPE.AudioBounceLighting,
            SpectrumKeyboardBacklightEffectType.AudioRipple => LENOVO_SPECTRUM_EFFECT_TYPE.AudioRippleLighting,
            SpectrumKeyboardBacklightEffectType.ColorChange => LENOVO_SPECTRUM_EFFECT_TYPE.ColorChange,
            SpectrumKeyboardBacklightEffectType.ColorPulse => LENOVO_SPECTRUM_EFFECT_TYPE.ColorPulse,
            SpectrumKeyboardBacklightEffectType.ColorWave => LENOVO_SPECTRUM_EFFECT_TYPE.ColorWave,
            SpectrumKeyboardBacklightEffectType.Rain => LENOVO_SPECTRUM_EFFECT_TYPE.Rain,
            SpectrumKeyboardBacklightEffectType.RainbowScrew => LENOVO_SPECTRUM_EFFECT_TYPE.ScrewRainbow,
            SpectrumKeyboardBacklightEffectType.RainbowWave => LENOVO_SPECTRUM_EFFECT_TYPE.RainbowWave,
            SpectrumKeyboardBacklightEffectType.Ripple => LENOVO_SPECTRUM_EFFECT_TYPE.Ripple,
            SpectrumKeyboardBacklightEffectType.Smooth => LENOVO_SPECTRUM_EFFECT_TYPE.Smooth,
            SpectrumKeyboardBacklightEffectType.Type => LENOVO_SPECTRUM_EFFECT_TYPE.TypeLighting,
            _ => throw new ArgumentException(nameof(effect.Type))
        };

        var speed = effect.Speed switch
        {
            SpectrumKeyboardBacklightSpeed.Speed1 => LENOVO_SPECTRUM_SPEED.Speed1,
            SpectrumKeyboardBacklightSpeed.Speed2 => LENOVO_SPECTRUM_SPEED.Speed2,
            SpectrumKeyboardBacklightSpeed.Speed3 => LENOVO_SPECTRUM_SPEED.Speed3,
            _ => LENOVO_SPECTRUM_SPEED.None
        };

        var direction = effect.Direction switch
        {
            SpectrumKeyboardBacklightDirection.LeftToRight => LENOVO_SPECTRUM_DIRECTION.LeftToRight,
            SpectrumKeyboardBacklightDirection.RightToLeft => LENOVO_SPECTRUM_DIRECTION.RightToLeft,
            SpectrumKeyboardBacklightDirection.BottomToTop => LENOVO_SPECTRUM_DIRECTION.BottomToTop,
            SpectrumKeyboardBacklightDirection.TopToBottom => LENOVO_SPECTRUM_DIRECTION.TopToBottom,
            _ => LENOVO_SPECTRUM_DIRECTION.None
        };

        var clockwiseDirection = effect.ClockwiseDirection switch
        {
            SpectrumKeyboardBacklightClockwiseDirection.Clockwise => LENOVO_SPECTRUM_CLOCKWISE_DIRECTION.Clockwise,
            SpectrumKeyboardBacklightClockwiseDirection.CounterClockwise => LENOVO_SPECTRUM_CLOCKWISE_DIRECTION.CounterClockwise,
            _ => LENOVO_SPECTRUM_CLOCKWISE_DIRECTION.None
        };

        var colorMode = effect.Type switch
        {
            SpectrumKeyboardBacklightEffectType.Always => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.ColorChange when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.ColorPulse when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.ColorWave when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.Rain when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.Smooth when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.Ripple when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.Type when effect.Colors.Length != 0 => LENOVO_SPECTRUM_COLOR_MODE.ColorList,
            SpectrumKeyboardBacklightEffectType.ColorChange => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            SpectrumKeyboardBacklightEffectType.ColorPulse => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            SpectrumKeyboardBacklightEffectType.ColorWave => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            SpectrumKeyboardBacklightEffectType.Rain => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            SpectrumKeyboardBacklightEffectType.Smooth => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            SpectrumKeyboardBacklightEffectType.Ripple => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            SpectrumKeyboardBacklightEffectType.Type => LENOVO_SPECTRUM_COLOR_MODE.RandomColor,
            _ => LENOVO_SPECTRUM_COLOR_MODE.None
        };

        var header = new LENOVO_SPECTRUM_EFFECT_HEADER(effectType, speed, direction, clockwiseDirection, colorMode);
        var colors = effect.Colors.Select(c => new LENOVO_SPECTRUM_COLOR(c.R, c.G, c.B)).ToArray();
        var keys = effect.Type.IsAllLightsEffect() ? [0x65] : effect.Keys;
var result = new LENOVO_SPECTRUM_EFFECT(header, index + 1, colors, keys);
        return result;
    }

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    _listener.Changed -= Listener_Changed;

                    // Stop Aurora and wait briefly so SetFeature cannot race a disposed handle.
                    try
                    {
                        StopAuroraIfNeededAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace("Error stopping Aurora during dispose", ex);
                    }

                    _auroraRefreshCancellationTokenSource?.Cancel();
                    _auroraRefreshCancellationTokenSource?.Dispose();
                    _auroraRefreshCancellationTokenSource = null;
                    _deviceHandle?.Dispose();
                    _deviceHandle = null;
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Error during SpectrumKeyboardBacklightController disposal", ex);
                }
            }
            _disposed = true;
        }
    }
}
