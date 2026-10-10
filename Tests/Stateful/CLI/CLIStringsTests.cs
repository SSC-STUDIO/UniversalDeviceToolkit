using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using FluentAssertions;
using UniversalDeviceToolkit.CLI;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Collection(TestCollections.Localization)]
[Trait("Category", TestCategories.Unit)]
public class CLIStringsTests
{
    private static readonly string ResourceBaseName =
        "UniversalDeviceToolkit.CLI.Resources.CLI.Resources";

    [Fact]
    public void AllKeys_NonEmptyInBase()
    {
        using var set = GetResourceSet(CultureInfo.InvariantCulture);

        foreach (var key in EnumerateKeys(set))
        {
            var value = set.GetObject(key);
            value.Should().NotBeNull($"base key '{key}' must be present");
            value!.ToString().Should().NotBeNullOrWhiteSpace($"base key '{key}' must have a translation");
        }
    }

    [Fact]
    public void ZhHans_ContainsSameKeysAsBase()
    {
        using var baseSet = GetResourceSet(CultureInfo.InvariantCulture);
        using var zhSet = GetResourceSet(CultureInfo.GetCultureInfo("zh-Hans"));

        var baseKeys = EnumerateKeys(baseSet);
        var zhKeys = EnumerateKeys(zhSet);

        zhKeys.Should().BeEquivalentTo(baseKeys, "zh-Hans satellite should mirror every base key");
    }

    [Fact]
    public void ZhHans_ValuesNotEmpty()
    {
        using var set = GetResourceSet(CultureInfo.GetCultureInfo("zh-Hans"));

        foreach (var key in EnumerateKeys(set))
        {
            var value = set.GetObject(key);
            value.Should().NotBeNull($"zh-Hans key '{key}' must be present");
            value!.ToString().Should().NotBeNullOrWhiteSpace($"zh-Hans key '{key}' must have a translation");
        }
    }

    [Fact]
    public void Get_ReturnsBaseEnglishWhenCultureIsEn()
    {
        WithCulture("en-US", () =>
        {
            var value = Strings.Get("CLI_Shell_RegisteredYes", "Shell is registered");
            value.Should().Be("Shell is registered");
        });
    }

    [Fact]
    public void Get_ReturnsChineseWhenCultureIsZhHans()
    {
        WithCulture("zh-Hans", () =>
        {
            var value = Strings.Get("CLI_Shell_RegisteredYes", "Shell is registered");
            value.Should().Be("Shell \u5DF2\u6CE8\u518C");
        });
    }

    [Fact]
    public void Get_ReturnsFallbackForUnknownKey()
    {
        var value = Strings.Get("CLI_NotARealKey_xyz", "fallback-value");
        value.Should().Be("fallback-value");
    }

    [Fact]
    public void Get_WithArgs_FormatsTemplate()
    {
        var value = Strings.Get(
            "CLI_Error_QaNameOrList_Required",
            "{0} or --list should be specified",
            "name");

        value.Should().Contain("name");
        value.Should().Contain("--list");
    }

    [Fact]
    public void Get_AllLoadingKeys_NonEmpty()
    {
        var keys = new[]
        {
            "CLI_Loading_ListFeatures",
            "CLI_Loading_ListQuickActions",
            "CLI_Loading_GetAppStatus",
            "CLI_Loading_GetNetworkAccelerationStatus",
            "CLI_Loading_StartNetworkAcceleration",
            "CLI_Loading_StopNetworkAcceleration",
            "CLI_Loading_RunNetworkDiagnostics",
            "CLI_Loading_Default",
            "CLI_Loading_QuickAction",
            "CLI_Loading_InstallShell",
            "CLI_Loading_UninstallShell",
            "CLI_Loading_SetRGBPreset",
            "CLI_Loading_GetSpectrumBrightness",
            "CLI_Loading_SetFeatureValue",
            "CLI_Loading_SetSpectrumBrightness",
            "CLI_Loading_ListFeatureValues",
            "CLI_Loading_IsShellInstalled",
            "CLI_Loading_GetFeatureValue",
            "CLI_Loading_SetSpectrumProfile",
            "CLI_Loading_GetRGBPreset",
            "CLI_Loading_GetSpectrumProfile",
            "CLI_Loading_IsShellRegistered",
        };

        keys.Should().HaveCount(22, "GetLoadingMessage emits one key per OperationType plus a default");

        foreach (var key in keys)
        {
            var value = Strings.Get(key, string.Empty);
            value.Should().NotBeNullOrWhiteSpace($"loading key '{key}' should resolve to a non-empty value");
        }
    }

    [Fact]
    public void Get_AllCommandDescriptionKeys_NonEmpty()
    {
        var usedKeys = new[]
        {
            "CLI_Header_RootCommandDescription",
            "CLI_Command_Status_Description",
            "CLI_Command_QuickAction_Description",
            "CLI_Argument_QaName_Description",
            "CLI_Option_QaList_Description",
            "CLI_Error_QaNameOrList_Required",
            "CLI_Command_Feature_Description",
            "CLI_Command_GetFeature_Description",
            "CLI_Command_SetFeature_Description",
            "CLI_Option_FeatureList_Description",
            "CLI_Argument_FeatureName_Description",
            "CLI_Argument_FeatureValue_Description",
            "CLI_Option_FeatureValueList_Description",
            "CLI_Error_FeatureSubcommandOrList_Required",
            "CLI_Error_FeatureNameOrList_Required",
            "CLI_Command_Spectrum_Description",
            "CLI_Command_SpectrumProfile_Description",
            "CLI_Command_SpectrumBrightness_Description",
            "CLI_Command_GetSpectrumProfile_Description",
            "CLI_Command_SetSpectrumProfile_Description",
            "CLI_Command_GetSpectrumBrightness_Description",
            "CLI_Command_SetSpectrumBrightness_Description",
            "CLI_Argument_SpectrumProfile_Description",
            "CLI_Argument_SpectrumBrightness_Description",
            "CLI_Command_RGB_Description",
            "CLI_Command_GetRGB_Description",
            "CLI_Command_SetRGB_Description",
            "CLI_Argument_RGBPreset_Description",
            "CLI_Command_Shell_Description",
            "CLI_Option_ShellStatus_Description",
            "CLI_Option_ShellInstall_Description",
            "CLI_Option_ShellUninstall_Description",
            "CLI_Option_ShellInstallStatus_Description",
            "CLI_Shell_RegisteredYes",
            "CLI_Shell_RegisteredNo",
            "CLI_Shell_InstalledYes",
            "CLI_Shell_InstalledNo",
            "CLI_Shell_InstallInitiated",
            "CLI_Shell_UninstallInitiated",
            "CLI_Shell_NoAction_Hint",
            "CLI_Shell_NoAction_Status",
            "CLI_Shell_NoAction_Install",
            "CLI_Shell_NoAction_Uninstall",
            "CLI_Shell_NoAction_InstallStatus",
            "CLI_Error_ShellOnlyOneAction",
            "CLI_Error_ShellAtLeastOneAction",
            "CLI_Command_Network_Description",
            "CLI_Option_NetworkStatus_Description",
            "CLI_Option_NetworkStart_Description",
            "CLI_Option_NetworkStop_Description",
            "CLI_Option_NetworkDiagnostics_Description",
            "CLI_Error_NetworkOnlyOneAction",
            "CLI_Error_NetworkAtLeastOneAction",
            "CLI_IpcError_ConnectFailed",
            "CLI_IpcError_MissingReturnMessage",
            "CLI_IpcError_AuthChallengeFailed",
            "CLI_IpcError_UnknownFailure",
        };

        foreach (var key in usedKeys)
        {
            var value = Strings.Get(key, string.Empty);
            value.Should().NotBeNullOrWhiteSpace($"used key '{key}' must resolve via fallback even on a stripped satellite lookup");
        }
    }

    [Fact]
    public void ResourceBaseName_IsStable()
    {
        typeof(Strings).Assembly
            .GetManifestResourceNames()
            .Should()
            .Contain(ResourceBaseName + ".resources",
                "the .NET SDK should embed the base resx under the expected namespace");
    }

    private static ResourceSet GetResourceSet(CultureInfo culture)
    {
        var manager = new ResourceManager(ResourceBaseName, typeof(Strings).Assembly);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: true);
        set.Should().NotBeNull($"resource set for '{culture.Name}' must be discoverable");
        return set!;
    }

    private static string[] EnumerateKeys(ResourceSet set)
    {
        var keys = new List<string>();
        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string s && !string.IsNullOrEmpty(s))
                keys.Add(s);
        }
        return keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray();
    }

    private static void WithCulture(string name, Action action)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
