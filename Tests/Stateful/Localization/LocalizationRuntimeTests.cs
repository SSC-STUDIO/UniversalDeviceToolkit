using System.Collections;
using System.Globalization;
using System.Resources;
using System.Xml;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.CLI;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Localization;

[Collection(TestCollections.Localization)]
[Trait("Category", TestCategories.Unit)]
public sealed class LocalizationRuntimeTests : IDisposable
{
    private readonly string _appDataDirectory;
    private readonly string? _previousAppDataOverride;
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private readonly CultureInfo? _previousDefaultCulture;
    private readonly CultureInfo? _previousDefaultUiCulture;

    public LocalizationRuntimeTests()
    {
        _appDataDirectory = Path.Combine(Path.GetTempPath(), $"udt-localization-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_appDataDirectory);
        _previousAppDataOverride = Environment.GetEnvironmentVariable("UDT_APPDATA_OVERRIDE");
        Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", _appDataDirectory);
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        _previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    }

    [Fact]
    public void SupportedCultures_AreUniqueAndUseCanonicalNames()
    {
        var cultures = LocalizationCatalog.SupportedCultures;

        cultures.Should().HaveCount(25);
        cultures.Select(culture => culture.Name)
            .Should().OnlyHaveUniqueItems();
        cultures.Should().Contain(culture => culture.Name == "zh-Hans");
        cultures.Should().Contain(culture => culture.Name == "zh-Hant");
        cultures.Should().Contain(culture => culture.Name == "uz-Latn-UZ");
    }

    [Theory]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("de-DE", "de")]
    [InlineData("pt-PT", "pt")]
    [InlineData("not-a-real-culture", "en")]
    public void NormalizeCulture_UsesExactThenParentAndEnglishFallback(string input, string expected)
    {
        LocalizationCatalog.NormalizeCulture(input).Name.Should().Be(expected);
    }

    [Theory]
    [InlineData("de", false)]
    [InlineData("zh-Hans", true)]
    [InlineData("zh-Hant", true)]
    public void FallbackChain_OnlyUsesChineseForChineseRequests(string cultureName, bool allowChinese)
    {
        var chain = LocalizationCatalog.GetFallbackChain(new CultureInfo(cultureName)).ToArray();

        chain.Should().Contain(culture => culture.Name == "en");
        if (allowChinese)
            chain.Should().Contain(culture => culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
        else
            chain.Should().NotContain(culture => culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ar", true)]
    [InlineData("ar-SA", true)]
    [InlineData("de", false)]
    [InlineData("zh-Hant", false)]
    public void IsRightToLeft_UsesTheNormalizedCultureTextInfo(string cultureName, bool expected)
    {
        LocalizationCatalog.IsRightToLeft(new CultureInfo(cultureName)).Should().Be(expected);
    }

    [Fact]
    public void Initialize_WithoutOverride_ReadsPersistedCulture()
    {
        File.WriteAllText(LocalizationRuntime.LanguageFilePath, "fr-FR");

        var culture = LocalizationRuntime.Initialize(preferredCultureName: null, persist: false);

        culture.Name.Should().Be("fr");
        LocalizationRuntime.CurrentCulture.Name.Should().Be("fr");
        CultureInfo.CurrentUICulture.Name.Should().Be("fr");
    }

    [Fact]
    public async Task SetCulture_PersistsCanonicalCultureName()
    {
        await LocalizationRuntime.SetCultureAsync("pt-br", persist: true);

        File.ReadAllText(LocalizationRuntime.LanguageFilePath).Should().Be("pt-BR");
        LocalizationRuntime.CurrentCulture.Name.Should().Be("pt-BR");
    }

    [Fact]
    public async Task SetCulture_NormalizesElectronChineseCodeToHostCatalogCulture()
    {
        var culture = await LocalizationRuntime.SetCultureAsync("zh-CN", persist: true);

        culture.Name.Should().Be("zh-Hans");
        File.ReadAllText(LocalizationRuntime.LanguageFilePath).Should().Be("zh-Hans");
        CultureInfo.CurrentUICulture.Name.Should().Be("zh-Hans");
    }

    [Fact]
    public void ResourceManagerLocalizer_ResolvesEnglishAndChineseResources()
    {
        var localizer = new ResourceManagerStringLocalizer(new ResourceManager(
            "UniversalDeviceToolkit.CLI.Resources.CLI.Resources",
            typeof(Strings).Assembly));

        localizer.CurrentCulture = new CultureInfo("en-US");
        localizer.GetString("CLI_Shell_RegisteredYes").Should().Be("Shell is registered");

        localizer.CurrentCulture = new CultureInfo("zh-Hans");
        localizer.GetString("CLI_Shell_RegisteredYes").Should().Be("Shell \u5DF2\u6CE8\u518C");

        localizer.CurrentCulture = new CultureInfo("de");
        localizer.GetString("CLI_Shell_RegisteredYes").Should().Be("Shell ist registriert");

        localizer.CurrentCulture = new CultureInfo("is");
        localizer.GetString("CLI_Shell_RegisteredYes").Should().Be("Shell is registered");
    }

    [Fact]
    public void CliResourceSets_HaveMatchingKeysAndPlaceholders()
    {
        var manager = new ResourceManager(
            "UniversalDeviceToolkit.CLI.Resources.CLI.Resources",
            typeof(Strings).Assembly);
        using var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
        using var simplified = manager.GetResourceSet(new CultureInfo("zh-Hans"), true, false);

        neutral.Should().NotBeNull();
        simplified.Should().NotBeNull();

        var neutralValues = ReadValues(neutral!);
        var simplifiedValues = ReadValues(simplified!);
        simplifiedValues.Keys.Should().BeEquivalentTo(neutralValues.Keys);

        foreach (var key in neutralValues.Keys)
            ExtractFormatIndexes(simplifiedValues[key]).Should().BeEquivalentTo(ExtractFormatIndexes(neutralValues[key]));
    }

    [Theory]
    [InlineData("Apps/CrossPlatformCLI/Resources/Resource.resx", "Apps/CrossPlatformCLI/Resources/Resource.zh-Hans.resx")]
    public void HostResourceFiles_HaveMatchingKeysAndPlaceholders(string neutralRelativePath, string chineseRelativePath)
    {
        var root = RepositoryPaths.FindRoot();
        var neutral = ReadResxValues(Path.Combine(root, neutralRelativePath));
        var chinese = ReadResxValues(Path.Combine(root, chineseRelativePath));

        chinese.Keys.Should().BeEquivalentTo(neutral.Keys);
        foreach (var key in neutral.Keys)
        {
            neutral[key].Should().NotBeNullOrWhiteSpace($"neutral key '{key}' must have a value");
            chinese[key].Should().NotBeNullOrWhiteSpace($"zh-Hans key '{key}' must have a value");
            ExtractFormatIndexes(chinese[key]).Should().BeEquivalentTo(ExtractFormatIndexes(neutral[key]));
        }
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultUiCulture;
        Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", _previousAppDataOverride);

        try
        {
            if (Directory.Exists(_appDataDirectory))
                Directory.Delete(_appDataDirectory, recursive: true);
        }
        catch
        {
            // Test cleanup must not hide the assertion that already ran.
        }
    }

    private static Dictionary<string, string> ReadValues(ResourceSet set) =>
        set.Cast<DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is not null)
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value!.ToString()!);

    private static Dictionary<string, string> ReadResxValues(string path)
    {
        var document = new XmlDocument();
        document.Load(path);
        return document.SelectNodes("/root/data")!
            .Cast<XmlElement>()
            .ToDictionary(
                element => element.GetAttribute("name"),
                element => element.SelectSingleNode("value")?.InnerText ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static int[] ExtractFormatIndexes(string value)
    {
        var indexes = new List<int>();
        for (var index = 0; index < value.Length - 1; index++)
        {
            if (value[index] != '{' || !char.IsDigit(value[index + 1]))
                continue;

            var end = index + 1;
            while (end < value.Length && char.IsDigit(value[end]))
                end++;

            if (end < value.Length && value[end] == '}'
                && int.TryParse(value[(index + 1)..end], out var argumentIndex))
            {
                indexes.Add(argumentIndex);
            }

            index = end;
        }

        return indexes.OrderBy(index => index).ToArray();
    }
}
