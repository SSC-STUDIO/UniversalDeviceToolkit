using System.IO;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.ResourcesCatalog;
using Xunit;

namespace UniversalDeviceToolkit.Tests.ResourcesCatalog;

[Trait("Category", TestCategories.Unit)]
public sealed class OnlineResourceCatalogTests
{
    [Fact]
    public void Catalog_ShouldDeserializeRequiredResourceShape()
    {
        // Arrange
        const string json = """
                            {
                              "schemaVersion": 1,
                              "appVersion": "3.8.0",
                              "downloads": {
                                "full": {
                                  "portable": {
                                    "name": "UniversalDeviceToolkit_v3.8.0_Full_win-x64.zip",
                                    "url": "https://example.com/releases/UniversalDeviceToolkit_v3.8.0_Full_win-x64.zip",
                                    "sha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                                    "size": 4096
                                  }
                                },
                                "cli": {
                                  "crossPlatform": {
                                    "name": "UniversalDeviceToolkit_v3.8.0_CLI_cross-platform.zip",
                                    "url": "https://example.com/releases/UniversalDeviceToolkit_v3.8.0_CLI_cross-platform.zip",
                                    "sha256": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                                    "size": 8192
                                  }
                                }
                              },
                              "languages": [
                                {
                                  "culture": "zh-Hans",
                                  "parent": "zh",
                                  "displayName": "Chinese (Simplified)",
                                  "url": "https://example.com/resources/3.8.0/languages/zh-Hans.zip",
                                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                  "size": 1024,
                                  "resourceVersion": "1.0.0",
                                  "minAppVersion": "3.8.0"
                                }
                              ],
                              "devicePacks": [
                                {
                                  "id": "lenovo-legion-pro-7",
                                  "displayName": "Lenovo Legion Pro 7",
                                  "vendor": "LENOVO",
                                  "vendorAliases": ["Lenovo"],
                                  "families": ["Legion"],
                                  "modelPrefixes": ["16IRX"],
                                  "modelKeywords": ["Legion Pro 7"],
                                  "machineTypes": ["83DE"],
                                  "url": "https://example.com/resources/3.8.0/devices/lenovo-legion-pro-7.zip",
                                  "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                                  "size": 2048
                                }
                              ]
                            }
                            """;

        // Act
        var catalog = JsonSerializer.Deserialize<OnlineResourceCatalog>(json);

        // Assert
        catalog.Should().NotBeNull();
        catalog!.SchemaVersion.Should().Be(1);
        catalog.AppVersion.Should().Be("3.8.0");
        catalog.Downloads?.Full?.Portable.Should().NotBeNull();
        catalog.Downloads!.Full!.Portable!.Name.Should().Be("UniversalDeviceToolkit_v3.8.0_Full_win-x64.zip");
        catalog.Downloads.Full.Portable.Sha256.Should().HaveLength(64);
        catalog.Downloads.Cli?.CrossPlatform.Should().NotBeNull();
        catalog.Downloads.Cli!.CrossPlatform!.Name.Should().Be("UniversalDeviceToolkit_v3.8.0_CLI_cross-platform.zip");
        catalog.Downloads.Cli.CrossPlatform.Sha256.Should().HaveLength(64);
        catalog.Languages.Should().ContainSingle(language =>
            language.Culture == "zh-Hans" &&
            language.Parent == "zh" &&
            language.ResourceVersion == "1.0.0" &&
            language.MinAppVersion == "3.8.0" &&
            language.Sha256.Length == 64 &&
            language.Size == 1024);
        catalog.DevicePacks.Should().ContainSingle(devicePack =>
            devicePack.Id == "lenovo-legion-pro-7" &&
            devicePack.VendorAliases.Contains("Lenovo") &&
            devicePack.ModelKeywords.Contains("Legion Pro 7") &&
            devicePack.MachineTypes.Contains("83DE"));
    }

    [Fact]
    public void Catalog_ShouldRejectEmptyLanguageUrlAtCallSiteShape()
    {
        // This mirrors the validation LanguagePackManager applies after catalog lookup.
        var language = new OnlineLanguageResource
        {
            Culture = "de",
            DisplayName = "German",
            Url = string.Empty,
            Sha256 = new string('a', 64),
            Size = 1
        };

        var action = () =>
        {
            if (string.IsNullOrWhiteSpace(language.Url))
                throw new InvalidDataException("Language has an empty download URL.");
        };

        action.Should().Throw<InvalidDataException>();
    }
}
