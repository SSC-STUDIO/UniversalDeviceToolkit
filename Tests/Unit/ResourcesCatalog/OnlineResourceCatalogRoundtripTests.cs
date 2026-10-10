using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.ResourcesCatalog;
using Xunit;

namespace UniversalDeviceToolkit.Tests.ResourcesCatalog;

[Trait("Category", TestCategories.Unit)]
public sealed class OnlineResourceCatalogRoundtripTests
{
    [Fact]
    public void Catalog_ShouldRoundtripThroughJson()
    {
        var original = new OnlineResourceCatalog
        {
            SchemaVersion = 1,
            AppVersion = "1.0.0-test",
            Downloads = new OnlineDownloads
            {
                Full = new OnlineDownloadGroup
                {
                    Portable = new OnlineFileResource
                    {
                        Name = "Setup.exe",
                        Url = "https://example.com/setup.exe",
                        Sha256 = new string('a', 64),
                        Size = 4096
                    }
                }
            },
            Languages =
            [
                new OnlineLanguageResource { Culture = "en", DisplayName = "English", Url = "https://example.com/en.zip", Sha256 = new string('b', 64), Size = 1024 }
            ],
            DevicePacks =
            [
                new OnlineDevicePackResource { Id = "test-pack", DisplayName = "Test Pack", Vendor = "Test", Url = "https://example.com/pack.zip", Sha256 = new string('c', 64), Size = 2048 }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OnlineResourceCatalog>(json);

        deserialized.Should().NotBeNull();
        deserialized!.SchemaVersion.Should().Be(1);
        deserialized.AppVersion.Should().Be("1.0.0-test");
        deserialized.Downloads!.Full!.Portable!.Name.Should().Be("Setup.exe");
        deserialized.Languages.Should().ContainSingle(l => l.Culture == "en");
        deserialized.DevicePacks.Should().ContainSingle(p => p.Id == "test-pack");
    }

    [Fact]
    public void OnlineFileResource_Defaults_ShouldBeEmpty()
    {
        var file = new OnlineFileResource();
        file.Name.Should().BeEmpty();
        file.Url.Should().BeEmpty();
        file.Sha256.Should().BeEmpty();
        file.Size.Should().Be(0);
    }

    [Fact]
    public void OnlineLanguageResource_Defaults_ShouldBeEmpty()
    {
        var lang = new OnlineLanguageResource();
        lang.Culture.Should().BeEmpty();
        lang.DisplayName.Should().BeEmpty();
        lang.Url.Should().BeEmpty();
        lang.Sha256.Should().BeEmpty();
        lang.Size.Should().Be(0);
    }

    [Fact]
    public void OnlineDevicePackResource_Defaults_ShouldBeEmpty()
    {
        var pack = new OnlineDevicePackResource();
        pack.Id.Should().BeEmpty();
        pack.DisplayName.Should().BeEmpty();
        pack.Vendor.Should().BeEmpty();
        pack.VendorAliases.Should().BeEmpty();
        pack.Families.Should().BeEmpty();
        pack.ModelPrefixes.Should().BeEmpty();
        pack.ModelKeywords.Should().BeEmpty();
        pack.MachineTypes.Should().BeEmpty();
    }

    [Fact]
    public void OnlineDownloads_NullGroups_ShouldDeserializeCorrectly()
    {
        const string json = """
                            {
                              "schemaVersion": 1,
                              "appVersion": "0.0.0",
                              "downloads": {
                                "full": null,
                                "online": null,
                                "cli": null
                              }
                            }
                            """;
        var catalog = JsonSerializer.Deserialize<OnlineResourceCatalog>(json);
        catalog.Should().NotBeNull();
        catalog!.Downloads.Should().NotBeNull();
        catalog.Downloads!.Full.Should().BeNull();
        catalog.Downloads.Online.Should().BeNull();
        catalog.Downloads.Cli.Should().BeNull();
    }
}
