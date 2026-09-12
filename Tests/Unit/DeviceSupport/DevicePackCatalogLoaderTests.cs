using System.IO;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Hardware;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public sealed class DevicePackCatalogLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"udt-catalog-loader-{Guid.NewGuid():N}");

    public DevicePackCatalogLoaderTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Load_WhenExplicitArrayCatalogExists_ShouldReturnPacks()
    {
        var path = Path.Combine(_root, "device-packs.json");
        File.WriteAllText(path, """
            [
              { "id": "asus-basic", "displayName": "ASUS Basic", "vendor": "ASUS" }
            ]
            """);

        var packs = DevicePackCatalogLoader.Load(path);

        packs.Should().ContainSingle(pack => pack.Id == "asus-basic" && pack.Vendor == "ASUS");
    }

    [Fact]
    public void Load_WhenCatalogIsWrappedObject_ShouldReadDevicePacksProperty()
    {
        var path = Path.Combine(_root, "wrapped.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "devicePacks": [
                { "id": "hp-basic", "displayName": "HP Basic", "vendor": "HP" }
              ]
            }
            """);

        var packs = DevicePackCatalogLoader.Load(path);

        packs.Should().ContainSingle(pack => pack.Id == "hp-basic");
    }

    [Fact]
    public void Load_WhenExplicitCatalogExistsButIsInvalid_ShouldNotWalkToAncestorCatalog()
    {
        var ancestorResources = Path.Combine(_root, "resources");
        Directory.CreateDirectory(ancestorResources);
        File.WriteAllText(Path.Combine(ancestorResources, "device-packs.json"), """
            [
              { "id": "should-not-load", "displayName": "Ancestor", "vendor": "*" }
            ]
            """);

        var nested = Path.Combine(_root, "app", "resources");
        Directory.CreateDirectory(nested);
        var invalid = Path.Combine(nested, "device-packs.json");
        File.WriteAllText(invalid, "{ not-json");

        var packs = DevicePackCatalogLoader.Load(invalid);

        packs.Should().BeEmpty();
    }

    [Fact]
    public void Load_WhenExplicitPathIsDirectory_ShouldReadNestedCatalog()
    {
        var resources = Path.Combine(_root, "bundle", "resources");
        Directory.CreateDirectory(resources);
        File.WriteAllText(Path.Combine(resources, "device-packs.json"), """
            [
              { "id": "msi-basic", "displayName": "MSI Basic", "vendor": "MSI" }
            ]
            """);

        var packs = DevicePackCatalogLoader.Load(Path.Combine(_root, "bundle"));

        packs.Should().ContainSingle(pack => pack.Id == "msi-basic");
    }

    [Fact]
    public void Load_WhenExplicitCatalogIsEmptyArray_ShouldReturnEmpty()
    {
        var path = Path.Combine(_root, "empty.json");
        File.WriteAllText(path, "[]");

        DevicePackCatalogLoader.Load(path).Should().BeEmpty();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
