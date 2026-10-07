using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UniversalDeviceToolkit.Windows;

internal static class UpdatePackage
{
    internal static (JsonElement Release, JsonElement Installer, JsonElement Manifest)? SelectLatest(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array) return null;
        JsonElement? latest = null;
        Version? latestVersion = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object
                || release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True
                || release.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True
                || !release.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String
                || !Version.TryParse(tag.GetString()?.TrimStart('v', 'V'), out var version)) continue;
            if (latestVersion != null && version <= latestVersion) continue;
            latest = release;
            latestVersion = version;
        }
        if (latest is not { } selectedRelease) return null;
        if (!selectedRelease.TryGetProperty("assets", out var assets) || Select(assets) is not { } package
            || !HasDownloadUrl(package.Installer) || !HasDownloadUrl(package.Manifest))
            throw new InvalidDataException($"The latest release {selectedRelease.GetProperty("tag_name").GetString()} does not contain a compatible WebView2 installer and SHA256 manifest.");
        return (selectedRelease, package.Installer, package.Manifest);
    }

    private static bool HasDownloadUrl(JsonElement asset) => asset.TryGetProperty("browser_download_url", out var url)
        && url.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(url.GetString());

    internal static (JsonElement Installer, JsonElement Manifest)? Select(JsonElement assets)
    {
        if (assets.ValueKind != JsonValueKind.Array) return null;
        var entries = assets.EnumerateArray().ToArray();
        // Explicit shell names prevent a mixed or historical release from
        // silently moving a WebView2 installation back to Electron.
        foreach (var prefix in new[] { "UniversalDeviceToolkitWebView2Setup-", "UniversalDeviceToolkitLightweightSetup-" })
        {
            foreach (var installer in entries)
            {
                var name = Name(installer);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || name.IndexOfAny(['/', '\\']) >= 0) continue;
                var manifest = entries.FirstOrDefault(asset => Name(asset).Equals(name + ".sha256", StringComparison.OrdinalIgnoreCase));
                if (manifest.ValueKind != JsonValueKind.Object)
                    manifest = entries.FirstOrDefault(asset => Regex.IsMatch(Name(asset), @"^UniversalDeviceToolkit_v[^/\\]+_SHA256\.txt$", RegexOptions.IgnoreCase));
                if (manifest.ValueKind == JsonValueKind.Object) return (installer, manifest);
            }
        }
        return null;
    }

    private static string Name(JsonElement asset) => asset.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
        ? name.GetString() ?? "" : "";

    internal static string? ReadHash(string manifest, string assetName)
    {
        foreach (var line in manifest.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line.Trim(), @"^([a-fA-F0-9]{64})\s+\*?(.+)$");
            if (match.Success && match.Groups[2].Value.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                return match.Groups[1].Value.ToLowerInvariant();
        }
        return null;
    }

    internal static async Task CopyVerifiedAsync(Stream input, string destination, string expectedHash, Action<long> progress)
    {
        var partial = destination + ".partial";
        try
        {
            // Close the writer before reopening or moving the file on Windows.
            await using (var output = File.Create(partial))
            {
                var buffer = new byte[128 * 1024];
                long received = 0;
                int read;
                while ((read = await input.ReadAsync(buffer)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    received += read;
                    progress(received);
                }
            }
            if (!await MatchesHashAsync(partial, expectedHash))
                throw new IOException("Update package integrity check failed.");
            File.Move(partial, destination, true);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    internal static async Task<bool> MatchesHashAsync(string path, string expectedHash)
    {
        await using var input = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(input)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
