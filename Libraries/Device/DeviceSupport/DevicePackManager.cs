using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.ResourcesCatalog;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.DeviceSupport;

public sealed class DevicePackManager(OnlineResourceCatalogClient resourceCatalogClient)
{
    private const string ManifestFileName = "device-pack.json";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] AllowedExtensions = [".json"];
    private static readonly string[] BlockedExtensions =
    [
        ".exe", ".dll", ".msi", ".msp", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".js", ".jar", ".scr", ".com", ".sys"
    ];

    public string DevicePacksRoot => Path.Combine(Folders.AppData, "device-packs");

    public bool IsInstalled(string packId)
    {
        if (!PathSecurity.IsValidPluginId(packId))
            return false;

        var manifestPath = GetInstalledManifestPath(packId);
        return File.Exists(manifestPath);
    }

    public DeviceSupportCatalog GetInstalledCatalog()
    {
        if (!Directory.Exists(DevicePacksRoot))
            return new DeviceSupportCatalog();

        var packs = Directory.EnumerateDirectories(DevicePacksRoot)
            .Where(IsInstalledPackDirectory)
            .Select(directory => ReadInstalledPack(Path.Combine(directory, ManifestFileName)))
            .Where(pack => pack is not null)
            .Cast<DevicePack>()
            .ToArray();

        return new DeviceSupportCatalog
        {
            SchemaVersion = 1,
            AppVersion = "installed",
            DevicePacks = packs
        };
    }

    public async Task<DevicePack> InstallAsync(string packId, IProgress<float>? progress = null, CancellationToken token = default)
    {
        if (!PathSecurity.IsValidPluginId(packId))
            throw new ArgumentException(Resource.Exception_InvalidFileName, nameof(packId));

        var catalog = await resourceCatalogClient.GetCatalogAsync(token).ConfigureAwait(false);
        var resource = catalog.DevicePacks.FirstOrDefault(pack =>
            pack.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));

        if (resource is null)
            throw ExceptionHelper.DevicePackNotAvailable(packId);

        ValidateResource(resource);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"{AppIdentity.CompactName}-device-pack-{packId}-{Guid.NewGuid():N}");
        var tempZipPath = Path.Combine(tempRoot, "device-pack.zip");
        var extractPath = Path.Combine(tempRoot, "extract");

        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractPath);

            await resourceCatalogClient.DownloadAndVerifyAsync(resource.Url, resource.Sha256, tempZipPath, progress, token).ConfigureAwait(false);
            ExtractDataOnlyZip(tempZipPath, extractPath);

            var manifestPath = Path.Combine(extractPath, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw ExceptionHelper.DevicePackNoManifest(packId, ManifestFileName);

            using var stream = File.OpenRead(manifestPath);
            var pack = await JsonSerializer.DeserializeAsync<DevicePack>(stream, JsonOptions, token).ConfigureAwait(false)
                       ?? throw ExceptionHelper.DevicePackManifestEmpty(packId);

            if (!pack.Id.Equals(resource.Id, StringComparison.OrdinalIgnoreCase))
                throw ExceptionHelper.DevicePackIdMismatch(resource.Id, pack.Id);

            if (!pack.Vendor.Equals(resource.Vendor, StringComparison.OrdinalIgnoreCase))
                throw ExceptionHelper.DevicePackVendorMismatch(resource.Vendor, pack.Vendor);

            var destination = GetInstalledPackDirectory(pack.Id);
            var pendingDestination = $"{destination}.pending";
            var backupDestination = $"{destination}.backup";

            TryDeleteDirectory(pendingDestination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            CopyDirectory(extractPath, pendingDestination);

            TryDeleteDirectory(backupDestination);
            if (Directory.Exists(destination))
                Directory.Move(destination, backupDestination);

            try
            {
                Directory.Move(pendingDestination, destination);
                TryDeleteDirectory(backupDestination);
            }
            catch
            {
                if (Directory.Exists(backupDestination) && !Directory.Exists(destination))
                    Directory.Move(backupDestination, destination);

                throw;
            }

            return pack;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public void Uninstall(string packId)
    {
        if (!PathSecurity.IsValidPluginId(packId))
            return;

        TryDeleteDirectory(GetInstalledPackDirectory(packId));
    }

    private static void ValidateResource(OnlineDevicePackResource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Url))
            throw ExceptionHelper.DevicePackUrlEmpty(resource.Id);

        if (string.IsNullOrWhiteSpace(resource.Sha256))
            throw ExceptionHelper.DevicePackSha256Missing(resource.Id);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = LltJson.CreateSettingsOptions();
        options.PropertyNameCaseInsensitive = true;
        return options;
    }

    private static bool IsInstalledPackDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        return PathSecurity.IsValidPluginId(name) &&
               !name.EndsWith(".pending", StringComparison.OrdinalIgnoreCase) &&
               !name.EndsWith(".backup", StringComparison.OrdinalIgnoreCase);
    }

    private string GetInstalledPackDirectory(string packId) =>
        Path.Combine(DevicePacksRoot, PathSecurity.SanitizeFileName(packId));

    private string GetInstalledManifestPath(string packId) =>
        Path.Combine(GetInstalledPackDirectory(packId), ManifestFileName);

    private static DevicePack? ReadInstalledPack(string manifestPath)
    {
        try
        {
            using var stream = File.OpenRead(manifestPath);
            return JsonSerializer.Deserialize<DevicePack>(stream, JsonOptions);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to read installed device pack: {manifestPath}", ex);

            return null;
        }
    }

    private static void ExtractDataOnlyZip(string zipPath, string destinationDirectory)
    {
        var destinationRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(destinationDirectory));

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Contains('\\'))
                throw ExceptionHelper.DevicePackWindowsPathSep(entry.FullName);

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw ExceptionHelper.DevicePackUnsafePath(entry.FullName);

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var extension = Path.GetExtension(entry.Name);
            if (BlockedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
                !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw ExceptionHelper.DevicePackUnsupportedFileType(entry.FullName);

            if ((entry.ExternalAttributes & 0xA0000000) == 0xA0000000)
                throw ExceptionHelper.DevicePackUnsafePath(entry.FullName);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, true);
        }
    }

    private static string EnsureTrailingDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(destinationPath);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, true);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to delete directory: {directory}", ex);
        }
    }
}
