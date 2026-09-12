using System;
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System;

public static partial class SystemTheme
{
    private const string REGISTRY_HIVE = "HKEY_CURRENT_USER";

    private const string PERSONALIZE_REGISTRY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string APPS_USE_LIGHT_THEME_REGISTRY_KEY = "AppsUseLightTheme";

    private const string DWM_REGISTRY_PATH = @"Software\Microsoft\Windows\DWM";
    private const string DWM_COLORIZATION_COLOR_REGISTRY_KEY = "ColorizationColor";

    private const string ACCENT_REGISTRY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string ACCENT_COLOR_MENU_REGISTRY_KEY = "AccentColorMenu";
    private const string PERSONALIZE_COLOR_PREVALENCE_REGISTRY_KEY = "ColorPrevalence";

    private const uint HWND_BROADCAST = 0xFFFF;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_THEMECHANGED = 0x0031;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public static bool IsDarkMode()
    {
        var registryValue = Registry.GetValue(REGISTRY_HIVE, PERSONALIZE_REGISTRY_PATH, APPS_USE_LIGHT_THEME_REGISTRY_KEY, -1);
        if (registryValue == -1)
            throw ExceptionHelper.CouldNotReadRegistrySetting(APPS_USE_LIGHT_THEME_REGISTRY_KEY);

        return registryValue == 0;
    }

    public static RGBColor GetColorizationColor()
    {
        var registryValue = Registry.GetValue(REGISTRY_HIVE, DWM_REGISTRY_PATH, DWM_COLORIZATION_COLOR_REGISTRY_KEY, -1);
        if (registryValue == -1)
            throw ExceptionHelper.CouldNotReadRegistrySetting(DWM_COLORIZATION_COLOR_REGISTRY_KEY);

        var bytes = BitConverter.GetBytes(registryValue);
        return new(bytes[2], bytes[1], bytes[0]);
    }

    public static RGBColor GetAccentColor()
    {
        var colorName = IsDarkMode() ? "SystemAccentLight2" : "SystemAccentDark1";
        return GetUxThemeImmersiveColor(colorName);
    }

    private static RGBColor GetUxThemeImmersiveColor(string name)
    {
        var colorType = GetImmersiveColorTypeFromName("Immersive" + name);

        if (colorType == 0xFFFFFFFF)
            throw ExceptionHelper.CouldNotGetColor(name);

        var activeColorSet = GetImmersiveUserColorSetPreference(false, false);
        var nativeColor = GetImmersiveColorFromColorSetEx(activeColorSet, colorType, false, 0);

        var r = (byte)((0x000000FF & nativeColor) >> 0);
        var g = (byte)((0x0000FF00 & nativeColor) >> 8);
        var b = (byte)((0x00FF0000 & nativeColor) >> 16);

        return new(r, g, b);
    }

    /// <summary>
    /// Writes the selected accent to the current user's Windows accent settings and notifies
    /// the shell. This is intentionally an explicit operation; callers should not invoke it
    /// during startup or while reacting to registry listeners.
    /// </summary>
    public static void SetAccentColor(RGBColor color)
    {
        var argb = unchecked((int)0xFF000000 |
                             (color.R << 16) |
                             (color.G << 8) |
                             color.B);

        Registry.SetValue(REGISTRY_HIVE, ACCENT_REGISTRY_PATH, ACCENT_COLOR_MENU_REGISTRY_KEY, argb,
            valueKind: Microsoft.Win32.RegistryValueKind.DWord);
        Registry.SetValue(REGISTRY_HIVE, DWM_REGISTRY_PATH, DWM_COLORIZATION_COLOR_REGISTRY_KEY, argb,
            valueKind: Microsoft.Win32.RegistryValueKind.DWord);
        Registry.SetValue(REGISTRY_HIVE, PERSONALIZE_REGISTRY_PATH, PERSONALIZE_COLOR_PREVALENCE_REGISTRY_KEY, 1,
            valueKind: Microsoft.Win32.RegistryValueKind.DWord);

        BroadcastSettingChange("ImmersiveColorSet");
        BroadcastSettingChange(DWM_REGISTRY_PATH);
        _ = SendMessageTimeout(
            new IntPtr(unchecked((int)HWND_BROADCAST)),
            WM_THEMECHANGED,
            UIntPtr.Zero,
            null,
            SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    private static void BroadcastSettingChange(string settingName)
    {
        _ = SendMessageTimeout(
            new IntPtr(unchecked((int)HWND_BROADCAST)),
            WM_SETTINGCHANGE,
            UIntPtr.Zero,
            settingName,
            SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    internal static IDisposable GetDarkModeListener(Action callback)
    {
        return Registry.ObserveValue(REGISTRY_HIVE, PERSONALIZE_REGISTRY_PATH, APPS_USE_LIGHT_THEME_REGISTRY_KEY, callback);
    }

    internal static IDisposable GetColorizationColorListener(Action callback)
    {
        return Registry.ObserveValue(REGISTRY_HIVE, DWM_REGISTRY_PATH, DWM_COLORIZATION_COLOR_REGISTRY_KEY, callback);
    }

    #region Native

    // ReSharper disable StringLiteralTypo

    [LibraryImport("uxtheme.dll", EntryPoint = "#95A")]
    private static partial uint GetImmersiveColorFromColorSetEx(uint immersiveColorSet, uint immersiveColorType, [MarshalAs(UnmanagedType.Bool)] bool ignoreHighContrast, uint highContrastCacheMode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#96W", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetImmersiveColorTypeFromName(string name);

    [LibraryImport("uxtheme.dll", EntryPoint = "#98A")]
    private static partial uint GetImmersiveUserColorSetPreference([MarshalAs(UnmanagedType.Bool)] bool forceCheckRegistry, [MarshalAs(UnmanagedType.Bool)] bool skipCheckOnFail);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string? lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    // ReSharper restore StringLiteralTypo

    #endregion
}
