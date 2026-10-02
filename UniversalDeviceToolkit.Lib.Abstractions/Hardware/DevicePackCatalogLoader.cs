using System.Text.Json;

namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Loads the shared data-only device-pack catalog for portable clients.
/// </summary>
public static class DevicePackCatalogLoader
{
    private const string CatalogFileName = "device-packs.json";
    /// <summary>Folder beside a deployed app (publish output, Electron payload).</summary>
    private const string ResourcesDirectoryName = "resources";
    /// <summary>Folder at the repository root, used when running from a source checkout.</summary>
    private const string RepositoryResourcesDirectoryName = "Resources";
    private const int AncestorWalkDepth = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyCollection<DevicePackDefinition> Load(string? catalogPath = null)
    {
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            var explicitExisted = false;
            foreach (var path in DistinctExisting(ExpandExplicitPath(catalogPath)))
            {
                explicitExisted = true;
                if (TryReadPacks(path, out var packs) && packs.Count > 0)
                    return packs;
            }

            if (explicitExisted)
                return [];
        }

        var foundInvalidAppLocal = false;
        foreach (var path in DistinctExisting(AppLocalPaths()))
        {
            if (TryReadPacks(path, out var packs) && packs.Count > 0)
                return packs;

            foundInvalidAppLocal = true;
        }

        // An app-local catalog that exists but is empty/invalid must not fall
        // through to a different device-packs.json higher in the tree.
        if (foundInvalidAppLocal)
            return [];

        foreach (var path in DistinctExisting(AncestorPaths()))
        {
            if (TryReadPacks(path, out var packs) && packs.Count > 0)
                return packs;
        }

        return [];
    }

    private static IEnumerable<string> AppLocalPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, ResourcesDirectoryName, CatalogFileName);

        var assemblyDirectory = Path.GetDirectoryName(typeof(DevicePackCatalogLoader).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            yield return Path.Combine(assemblyDirectory, ResourcesDirectoryName, CatalogFileName);

        yield return Path.Combine(Directory.GetCurrentDirectory(), ResourcesDirectoryName, CatalogFileName);
    }

    private static IEnumerable<string> ExpandExplicitPath(string? catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
            yield break;

        yield return catalogPath;
        if (Directory.Exists(catalogPath))
        {
            yield return Path.Combine(catalogPath, CatalogFileName);
            yield return Path.Combine(catalogPath, ResourcesDirectoryName, CatalogFileName);
        }
    }

    private static IEnumerable<string> AncestorPaths()
    {
        foreach (var root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            DirectoryInfo? directory;
            try
            {
                directory = new DirectoryInfo(root);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            for (var depth = 0; depth < AncestorWalkDepth && directory is not null; depth++, directory = directory.Parent)
            {
                yield return Path.Combine(directory.FullName, ResourcesDirectoryName, CatalogFileName);

                bool isRepositoryRoot;
                try
                {
                    isRepositoryRoot = directory.EnumerateFiles("*.sln").Any();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    break;
                }

                if (!isRepositoryRoot)
                    continue;

                // Case-sensitive file systems need the repository spelling as well.
                yield return Path.Combine(directory.FullName, RepositoryResourcesDirectoryName, CatalogFileName);
                break;
            }
        }
    }

    private static IEnumerable<string> DistinctExisting(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                continue;
            }

            if (!seen.Add(fullPath) || !File.Exists(fullPath))
                continue;

            yield return fullPath;
        }
    }

    private static bool TryReadPacks(string path, out IReadOnlyCollection<DevicePackDefinition> packs)
    {
        packs = [];
        try
        {
            var json = File.ReadAllText(path);
            var parsed = DeserializePacks(json);
            if (parsed is { Length: > 0 })
            {
                packs = parsed;
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }

        return false;
    }

    private static DevicePackDefinition[]? DeserializePacks(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (document.RootElement.ValueKind == JsonValueKind.Array)
            return document.RootElement.Deserialize<DevicePackDefinition[]>(JsonOptions);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in new[] { "devicePacks", "DevicePacks" })
        {
            if (!document.RootElement.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Array)
                continue;

            return property.Deserialize<DevicePackDefinition[]>(JsonOptions);
        }

        return null;
    }
}
