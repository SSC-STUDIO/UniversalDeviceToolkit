using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class SetupResourcesTests
{
    [Fact]
    public void UiAssets_AreServedAfterExtractionDirectoryBecomesUnavailable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "udt-setup-resource-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SetupResources resources;
        try
        {
            foreach (var file in new[] { "index.html", "styles.css", "renderer.mjs", "features.mjs", "i18n.mjs", "icon.png" })
                File.WriteAllText(Path.Combine(directory, file), "asset:" + file);
            resources = new SetupResources(directory);
        }
        finally { Directory.Delete(directory, true); }

        var page = resources.Get("https://setup.udt.local/index.html");
        using var reader = new StreamReader(page.Content);
        Assert.Equal(200, page.Status);
        Assert.Equal("text/html; charset=utf-8", page.ContentType);
        Assert.Equal("asset:index.html", reader.ReadToEnd());
        var module = resources.Get("https://setup.udt.local/renderer.mjs?v=1");
        using (module.Content) Assert.Equal("text/javascript; charset=utf-8", module.ContentType);
        foreach (var address in new[]
        {
            "https://setup.udt.local/files.json", "https://setup.udt.local/register.exe",
            "https://setup.udt.local/../installer-selection.ini", "https://other.local/index.html",
            "https://setup.udt.local:444/index.html", "http://setup.udt.local/index.html"
        })
        {
            var rejected = resources.Get(address);
            using (rejected.Content) Assert.Equal(404, rejected.Status);
        }
    }
}
