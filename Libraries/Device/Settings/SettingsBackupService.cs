using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Settings;

public sealed class SettingsBackupService
{
    public const int CurrentFormatVersion = 1;
    private const string ManifestName = "udt-settings-backup.json";

    public string Export(string destinationFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);
        var destinationPath = Path.GetFullPath(destinationFile);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        AtomicReplaceFile(destinationPath, tempPath =>
        {
            using var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create);
            var files = Directory.Exists(Folders.AppData)
                ? Directory.EnumerateFiles(Folders.AppData, "*.json", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName)
                : Enumerable.Empty<string>();
            foreach (var file in files)
                archive.CreateEntryFromFile(file, $"settings/{Path.GetFileName(file)}", CompressionLevel.Optimal);

            var entry = archive.CreateEntry(ManifestName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(JsonSerializer.Serialize(new Manifest(CurrentFormatVersion, DateTimeOffset.UtcNow)));
        });

        return destinationPath;
    }

    public string Import(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        using var archive = ZipFile.OpenRead(sourceFile);
        var manifestEntry = archive.GetEntry(ManifestName)
            ?? throw new InvalidDataException(Resource.SettingsBackup_MissingManifest);
        Manifest manifest;
        using (var reader = new StreamReader(manifestEntry.Open()))
            manifest = JsonSerializer.Deserialize<Manifest>(reader.ReadToEnd())
                ?? throw new InvalidDataException(Resource.SettingsBackup_InvalidManifest);
        if (manifest.FormatVersion > CurrentFormatVersion)
            throw new NotSupportedException(string.Format(
                Resource.SettingsBackup_NewerFormat,
                manifest.FormatVersion,
                CurrentFormatVersion));

        var settingsEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("settings/", StringComparison.Ordinal)
                            && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (settingsEntries.Length == 0)
            throw new InvalidDataException(Resource.SettingsBackup_InvalidManifest);

        foreach (var entry in settingsEntries)
        {
            if (!PathSecurity.IsValidFileName(entry.Name))
                throw new InvalidDataException(string.Format(
                    Resource.SettingsBackup_UnsafeEntry,
                    entry.FullName));
        }

        var backupsDir = Path.Combine(Folders.AppData, "Backups");
        Directory.CreateDirectory(backupsDir);
        var rollback = Path.Combine(backupsDir, $"pre-import-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        Export(rollback);

        var staging = Path.Combine(backupsDir, $"import-staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(staging);
            foreach (var entry in settingsEntries)
            {
                var stagedPath = Path.Combine(staging, entry.Name);
                if (!PathSecurity.IsPathWithinAllowedDirectory(stagedPath, staging))
                    throw new InvalidDataException(string.Format(
                        Resource.SettingsBackup_UnsafeEntry,
                        entry.FullName));
                entry.ExtractToFile(stagedPath, overwrite: true);
            }

            Directory.CreateDirectory(Folders.AppData);

            // Replace semantics (not merge): apply backup files, then remove AppData *.json
            // that are not present in the backup so stale settings cannot survive import.
            var importedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(staging, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                AtomicReplaceFromExistingFile(file, Path.Combine(Folders.AppData, name));
                importedNames.Add(name);
            }

            foreach (var existing in Directory.EnumerateFiles(Folders.AppData, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(existing);
                if (!importedNames.Contains(name))
                    File.Delete(existing);
            }

            return rollback;
        }
        catch
        {
            Restore(rollback);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Instance.WarningOnce(
                    "settings-backup-staging-cleanup",
                    $"Best-effort cleanup of settings backup staging failed: {staging}",
                    ex);
            }
        }
    }

    private static void Restore(string backupFile)
    {
        using var archive = ZipFile.OpenRead(backupFile);
        Directory.CreateDirectory(Folders.AppData);

        var restoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var restoreStaging = Path.Combine(Folders.AppData, "Backups", $"restore-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(restoreStaging);
        try
        {
            foreach (var entry in archive.Entries.Where(entry =>
                         entry.FullName.StartsWith("settings/", StringComparison.Ordinal)
                         && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                if (!PathSecurity.IsValidFileName(entry.Name))
                    continue;
                var stagedPath = Path.Combine(restoreStaging, entry.Name);
                if (!PathSecurity.IsPathWithinAllowedDirectory(stagedPath, restoreStaging))
                    continue;
                entry.ExtractToFile(stagedPath, overwrite: true);
                AtomicReplaceFromExistingFile(stagedPath, Path.Combine(Folders.AppData, entry.Name));
                restoredNames.Add(entry.Name);
            }

            // Mirror import replace semantics on rollback.
            foreach (var existing in Directory.EnumerateFiles(Folders.AppData, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(existing);
                if (!restoredNames.Contains(name))
                {
                    try { File.Delete(existing); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Log.Instance.Warning($"Failed to remove leftover settings file during restore: {existing}", ex);
                    }
                }
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(restoreStaging))
                    Directory.Delete(restoreStaging, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Instance.WarningOnce(
                    "settings-backup-restore-staging-cleanup",
                    $"Best-effort cleanup of settings restore staging failed: {restoreStaging}",
                    ex);
            }
        }
    }

    private static void AtomicReplaceFromExistingFile(string sourcePath, string destinationPath)
    {
        AtomicReplaceFile(destinationPath, tempPath => File.Copy(sourcePath, tempPath, overwrite: true));
    }

    /// <summary>
    /// Write to a sibling temp file, flush, then replace the destination so a crash
    /// cannot leave a torn JSON/zip at the live path.
    /// </summary>
    private static void AtomicReplaceFile(string destinationPath, Action<string> writeTempFile)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            writeTempFile(tempPath);
            using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                stream.Flush(flushToDisk: true);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Instance.Warning($"Failed to delete settings temp file: {tempPath}", ex);
                }
            }
        }
    }

    private sealed record Manifest(int FormatVersion, DateTimeOffset CreatedAtUtc);
}
