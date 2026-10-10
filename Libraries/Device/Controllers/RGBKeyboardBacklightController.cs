// #define MOCK_RGB

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using NeoSmart.AsyncLock;
using Windows.Win32;

namespace UniversalDeviceToolkit.Lib.Controllers
{
    public class RGBKeyboardBacklightController(RGBKeyboardSettings settings, VantageDisabler vantageDisabler)
    {
        private static readonly AsyncLock IoLock = new();

        private SafeFileHandle? _deviceHandle;

        private SafeFileHandle? DeviceHandle
        {
            get
            {
                if (ForceDisable)
                    return null;

                _deviceHandle ??= Devices.GetRGBKeyboard();
                return _deviceHandle;
            }
        }

        public bool ForceDisable { get; set; }

        public Task<bool> IsSupportedAsync()
        {
#if MOCK_RGB
            return Task.FromResult(true);
#else
            return Task.FromResult(DeviceHandle is not null);
#endif
        }

        public async Task SetLightControlOwnerAsync(bool enable, bool restorePreset = false)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
                try
                {
#if !MOCK_RGB
                    _ = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();
#endif

                    await ThrowIfVantageEnabled().ConfigureAwait(false);

#if !MOCK_RGB
                    await WMI.LenovoGameZoneData.SetLightControlOwnerAsync(enable ? 1 : 0).ConfigureAwait(false);
#endif

                    if (restorePreset)
                        await SetCurrentPresetAsync().ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Light control ownership: {enable}");

                    Log.Instance.Info($"Light control ownership set to {enable} [controller={nameof(RGBKeyboardBacklightController)}]");
                }
                catch (Exception ex)
                {
                    // Unsupported RGB hardware and Vantage ownership are expected on many systems.
                    // Keep them out of the exception log path so trace logs stay actionable.
                    if (ex is InvalidOperationException && IsExpectedOwnershipFailure(ex))
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Skipping RGB keyboard ownership: {ex.Message}");

                        return;
                    }

                    Log.Instance.Warning($"Can't take RGB keyboard ownership.", ex);

                    throw;
                }
            }
        }

        private static bool IsExpectedOwnershipFailure(Exception ex) =>
            ex.Message.Contains(Resource.Exception_RGBKeyboardUnsupported, StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains(Resource.Exception_CantManageWithVantage, StringComparison.OrdinalIgnoreCase);

        public async Task<RGBKeyboardBacklightState> GetStateAsync()
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                return settings.Store.State;
            }
        }

        public async Task SetStateAsync(RGBKeyboardBacklightState state)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                settings.Store.State = state;
                settings.SynchronizeStore();

                var selectedPreset = state.SelectedPreset;

                LENOVO_RGB_KEYBOARD_STATE str;
                if (selectedPreset == RGBKeyboardBacklightPreset.Off)
                    str = CreateOffState();
                else
                {
                    var presetDescription = state.Presets.GetValueOrDefault(selectedPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);
                    str = Convert(presetDescription);
                }

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"RGB state set to {selectedPreset}");

                Log.Instance.Info($"RGB state set to {selectedPreset} [controller={nameof(RGBKeyboardBacklightController)}]");

                await SendToDevice(str).ConfigureAwait(false);
            }
        }

        public async Task SetPresetAsync(RGBKeyboardBacklightPreset preset)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                var state = settings.Store.State;
                var presets = state.Presets;

                settings.Store.State = new(preset, presets);
                settings.SynchronizeStore();

                LENOVO_RGB_KEYBOARD_STATE str;
                if (preset == RGBKeyboardBacklightPreset.Off)
                    str = CreateOffState();
                else
                {
                    var presetDescription = state.Presets.GetValueOrDefault(preset, RGBKeyboardBacklightBacklightPresetDescription.Default);
                    str = Convert(presetDescription);
                }

                await SendToDevice(str).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"RGB preset set to {preset}");

                Log.Instance.Info($"RGB preset set to {preset} [controller={nameof(RGBKeyboardBacklightController)}]");
            }
        }

        public async Task<RGBKeyboardBacklightPreset> SetNextPresetAsync()
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                var state = settings.Store.State;

                var newPreset = state.SelectedPreset.Next();
                var presets = state.Presets;

                settings.Store.State = new(newPreset, presets);
                settings.SynchronizeStore();

                LENOVO_RGB_KEYBOARD_STATE str;
                if (newPreset == RGBKeyboardBacklightPreset.Off)
                    str = CreateOffState();
                else
                {
                    var presetDescription = state.Presets.GetValueOrDefault(newPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);
                    str = Convert(presetDescription);
                }

                await SendToDevice(str).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"RGB preset changed to {newPreset}");

                Log.Instance.Info($"RGB preset changed to {newPreset} [controller={nameof(RGBKeyboardBacklightController)}]");

                return newPreset;
            }
        }

        private async Task SetCurrentPresetAsync()
        {
#if !MOCK_RGB
            _ = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();
#endif

            await ThrowIfVantageEnabled().ConfigureAwait(false);

            var state = settings.Store.State;

            var preset = state.SelectedPreset;

            LENOVO_RGB_KEYBOARD_STATE str;
            if (preset == RGBKeyboardBacklightPreset.Off)
                str = CreateOffState();
            else
            {
                var presetDescription = state.Presets.GetValueOrDefault(preset, RGBKeyboardBacklightBacklightPresetDescription.Default);
                str = Convert(presetDescription);
            }

            await SendToDevice(str).ConfigureAwait(false);
        }

        private async Task ThrowIfVantageEnabled()
        {
            var vantageStatus = await vantageDisabler.GetStatusAsync().ConfigureAwait(false);
            if (vantageStatus == SoftwareStatus.Enabled)
                throw ExceptionHelper.CantManageWithVantage();
        }

        private unsafe Task SendToDevice(LENOVO_RGB_KEYBOARD_STATE str) => Task.Run(() =>
        {
#if !MOCK_RGB
            var handle = DeviceHandle ?? throw ExceptionHelper.RGBKeyboardUnsupported();

            var ptr = IntPtr.Zero;
            try
            {
                var size = Marshal.SizeOf<LENOVO_RGB_KEYBOARD_STATE>();
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(str, ptr, false);

                if (!PInvoke.HidD_SetFeature(handle.ToWin32Handle(), ptr.ToPointer(), (uint)size))
                    PInvokeExtensions.ThrowIfWin32Error("HidD_SetFeature");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
#endif
        });

        private static LENOVO_RGB_KEYBOARD_STATE CreateOffState()
        {
            return new()
            {
                Header = [0xCC, 0x16],
                Unused = new byte[13],
                Padding = 0,
                Effect = 0,
                WaveLTR = 0,
                WaveRTL = 0,
                Brightness = 0,
                Zone1Rgb = new byte[3],
                Zone2Rgb = new byte[3],
                Zone3Rgb = new byte[3],
                Zone4Rgb = new byte[3],
            };
        }

        private static LENOVO_RGB_KEYBOARD_STATE Convert(RGBKeyboardBacklightBacklightPresetDescription preset)
        {
            var result = new LENOVO_RGB_KEYBOARD_STATE
            {
                Header = [0xCC, 0x16],
                Unused = new byte[13],
                Padding = 0x0,
                Zone1Rgb = [0xFF, 0xFF, 0xFF],
                Zone2Rgb = [0xFF, 0xFF, 0xFF],
                Zone3Rgb = [0xFF, 0xFF, 0xFF],
                Zone4Rgb = [0xFF, 0xFF, 0xFF],
                Effect = preset.Effect switch
                {
                    RGBKeyboardBacklightEffect.Static => 1,
                    RGBKeyboardBacklightEffect.Breath => 3,
                    RGBKeyboardBacklightEffect.WaveRTL => 4,
                    RGBKeyboardBacklightEffect.WaveLTR => 4,
                    RGBKeyboardBacklightEffect.Smooth => 6,
                    _ => 0
                },
                WaveRTL = (byte)(preset.Effect == RGBKeyboardBacklightEffect.WaveRTL ? 1 : 0),
                WaveLTR = (byte)(preset.Effect == RGBKeyboardBacklightEffect.WaveLTR ? 1 : 0),
                Brightness = preset.Brightness switch
                {
                    RGBKeyboardBacklightBrightness.Low => 1,
                    RGBKeyboardBacklightBrightness.High => 2,
                    _ => 0
                }
            };


            if (preset.Effect != RGBKeyboardBacklightEffect.Static)
            {
                result.Speed = preset.Speed switch
                {
                    RGBKeyboardBacklightSpeed.Slowest => 1,
                    RGBKeyboardBacklightSpeed.Slow => 2,
                    RGBKeyboardBacklightSpeed.Fast => 3,
                    RGBKeyboardBacklightSpeed.Fastest => 4,
                    _ => 0
                };
            }

            if (preset.Effect is RGBKeyboardBacklightEffect.Static or RGBKeyboardBacklightEffect.Breath)
            {
                result.Zone1Rgb = [preset.Zone1.R, preset.Zone1.G, preset.Zone1.B];
                result.Zone2Rgb = [preset.Zone2.R, preset.Zone2.G, preset.Zone2.B];
                result.Zone3Rgb = [preset.Zone3.R, preset.Zone3.G, preset.Zone3.B];
                result.Zone4Rgb = [preset.Zone4.R, preset.Zone4.G, preset.Zone4.B];
            }

            return result;
        }
    }
}
