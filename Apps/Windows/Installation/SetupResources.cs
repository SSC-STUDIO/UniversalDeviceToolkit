namespace UniversalDeviceToolkit.Windows;

/// <summary>Serves the installer's fixed UI assets without sandbox filesystem access.</summary>
internal sealed class SetupResources(string directory)
{
    private readonly Dictionary<string, (string ContentType, byte[] Content)> _assets = new[]
    {
        ("index.html", "text/html; charset=utf-8"),
        ("styles.css", "text/css; charset=utf-8"),
        ("renderer.mjs", "text/javascript; charset=utf-8"),
        ("features.mjs", "text/javascript; charset=utf-8"),
        ("i18n.mjs", "text/javascript; charset=utf-8"),
        ("icon.png", "image/png")
    }.ToDictionary(asset => "/" + asset.Item1,
        asset => (asset.Item2, File.ReadAllBytes(Path.Combine(directory, asset.Item1))), StringComparer.Ordinal);

    internal (int Status, string ContentType, Stream Content) Get(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && uri.Scheme == "https" && uri.Host == "setup.udt.local" && uri.IsDefaultPort
            && _assets.TryGetValue(uri.AbsolutePath, out var asset))
            return (200, asset.ContentType, new MemoryStream(asset.Content, writable: false));
        return (404, "text/plain; charset=utf-8", new MemoryStream());
    }
}
