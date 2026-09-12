using System.Globalization;
using System.Resources;
using UniversalDeviceToolkit.Abstractions.Localization;

namespace UniversalDeviceToolkit.CLI;

public static class Strings
{
    private static readonly ResourceManagerStringLocalizer _localizer = new(
        new ResourceManager(
        "UniversalDeviceToolkit.CLI.Resources.CLI.Resources",
        typeof(Strings).Assembly));

    public static void ApplyCulture(CultureInfo culture) => _localizer.CurrentCulture = culture;

    public static string Get(string key, string fallback)
    {
        // Tests and embedders may change CurrentUICulture directly. Keep this
        // host localizer aligned without requiring an explicit ApplyCulture call.
        _localizer.CurrentCulture = CultureInfo.CurrentUICulture;
        return _localizer.GetString(key, fallback);
    }

    public static string Get(string key, string fallback, params object[] args)
    {
        var template = Get(key, fallback);
        return args.Length == 0 ? template : string.Format(CultureInfo.CurrentUICulture, template, args);
    }
}
