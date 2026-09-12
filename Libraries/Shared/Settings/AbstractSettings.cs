using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Shared.Logging;
using UniversalDeviceToolkit.Shared.Serialization;
using UniversalDeviceToolkit.Shared.Utils;

namespace UniversalDeviceToolkit.Shared.Settings;

/// <summary>
/// Cross-platform settings base class.
/// Extracted from Lib.Settings.AbstractSettings — replaces Serilog Log with SharedLog,
/// all other logic preserved verbatim.
/// </summary>
public abstract class AbstractSettings<T> where T : class, new()
{
    protected readonly JsonSerializerOptions JsonSerializerOptions;
    private readonly string _fileName;
    private readonly object _lock = new();
    private T? _cachedStore;
    private DateTime _lastLoadTime = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(5);
    /// <summary>Bumped on successful synchronize so in-flight loads do not clobber newer cache.</summary>
    private int _storeGeneration;

    protected virtual T Default => new();

    public T Store
    {
        get
        {
            lock (_lock)
            {
                return _cachedStore ??= LoadStore() ?? Default;
            }
        }
    }

    protected AbstractSettings(string filename)
    {
        if (!PathSecurity.IsValidFileName(filename))
            throw ExceptionHelper.InvalidSettingsFilename(filename, nameof(filename));

        JsonSerializerOptions = LltJson.CreateSettingsOptions();
        ConfigureJsonSerializerOptions(JsonSerializerOptions);

        _fileName = filename;

        var defaultSettingsPath = Path.Combine(Folders.AppData, _fileName);
        if (!PathSecurity.IsPathWithinAllowedDirectory(defaultSettingsPath, Folders.AppData))
            throw ExceptionHelper.SettingsPathEscapesAllowedDir(defaultSettingsPath);
    }

    /// <summary>
    /// Optional converters / options tweaks per settings file (e.g. automation polymorphism, legacy GUID migration).
    /// </summary>
    protected virtual void ConfigureJsonSerializerOptions(JsonSerializerOptions options) { }

    /// <summary>Override the on-disk settings file location (e.g. for tests that need a temp directory).
    /// Defaults to <c>Path.Combine(Folders.AppData, _fileName)</c>.</summary>
    protected virtual string SettingsFilePath => Path.Combine(Folders.AppData, _fileName);

    public void SynchronizeStore()
    {
        lock (_lock)
        {
            var settingsSerialized = JsonSerializer.Serialize(_cachedStore ?? Default, JsonSerializerOptions);
            AtomicWriteAllText(SettingsFilePath, settingsSerialized);
            _lastLoadTime = DateTime.UtcNow;
            _storeGeneration++;
        }
    }

    public async Task SynchronizeStoreAsync()
    {
        string settingsSerialized;
        lock (_lock)
        {
            settingsSerialized = JsonSerializer.Serialize(_cachedStore ?? Default, JsonSerializerOptions);
        }

        await AtomicWriteAllTextAsync(SettingsFilePath, settingsSerialized).ConfigureAwait(false);

        lock (_lock)
        {
            _lastLoadTime = DateTime.UtcNow;
            _storeGeneration++;
        }
    }

    /// <summary>
    /// Write via temp + replace so a crash mid-write cannot leave an empty/partial JSON file
    /// (which would force defaults and lose user settings on next load).
    /// </summary>
    private static void AtomicWriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
        }
    }

    private static async Task AtomicWriteAllTextAsync(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, contents).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
        }
    }

    public virtual T? LoadStore()
    {
        lock (_lock)
        {
            if (_cachedStore != null && DateTime.UtcNow - _lastLoadTime < _cacheDuration)
                return _cachedStore;

            T? store = null;
            try
            {
                var settingsSerialized = File.ReadAllText(SettingsFilePath);
                store = JsonSerializer.Deserialize<T>(settingsSerialized, JsonSerializerOptions);

                if (store is null)
                    TryBackup();
            }
            catch (FileNotFoundException)
            {
                if (SharedLog.IsTraceEnabled)
                    SharedLog.Trace($"Settings file {_fileName} not found; using defaults.");
            }
            catch (DirectoryNotFoundException)
            {
                if (SharedLog.IsTraceEnabled)
                    SharedLog.Trace($"Settings file {_fileName} directory not found; using defaults.");
            }
            catch (Exception ex)
            {
                if (SharedLog.IsTraceEnabled)
                    SharedLog.Trace($"Error loading settings for {_fileName}", ex);
                TryBackup();
            }

            _cachedStore = store;
            _lastLoadTime = DateTime.UtcNow;
            return store;
        }
    }

    public virtual async Task<T?> LoadStoreAsync()
    {
        int generation;
        lock (_lock)
        {
            if (_cachedStore != null && DateTime.UtcNow - _lastLoadTime < _cacheDuration)
                return _cachedStore;
            generation = _storeGeneration;
        }

        T? store = null;
        var loadSucceeded = false;

        try
        {
            var settingsSerialized = await File.ReadAllTextAsync(SettingsFilePath).ConfigureAwait(false);
            store = JsonSerializer.Deserialize<T>(settingsSerialized, JsonSerializerOptions);
            loadSucceeded = store is not null;

            if (store is null)
                TryBackup();
        }
        catch (FileNotFoundException)
        {
            if (SharedLog.IsTraceEnabled)
                SharedLog.Trace($"Settings file {_fileName} not found; using defaults.");
        }
        catch (DirectoryNotFoundException)
        {
            if (SharedLog.IsTraceEnabled)
                SharedLog.Trace($"Settings file {_fileName} directory not found; using defaults.");
        }
        catch (Exception ex)
        {
            if (SharedLog.IsTraceEnabled)
                SharedLog.Trace($"Error loading settings for {_fileName}", ex);
            TryBackup();
        }

        lock (_lock)
        {
            // A concurrent SynchronizeStore bumped generation — keep the newer in-memory cache.
            if (generation != _storeGeneration)
                return _cachedStore;

            // Do not wipe a populated cache with null after a transient IO/deserialize failure.
            if (!loadSucceeded && _cachedStore is not null)
                return _cachedStore;

            _cachedStore = store;
            _lastLoadTime = DateTime.UtcNow;
            return store;
        }
    }

    public void InvalidateCache()
    {
        lock (_lock)
        {
            _cachedStore = null;
            _lastLoadTime = DateTime.MinValue;
            _storeGeneration++;
        }
    }

    private void TryBackup()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return;

            var backupFileName = $"{Path.GetFileNameWithoutExtension(_fileName)}_backup_{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(_fileName)}";
            var backupFilePath = Path.Combine(Folders.AppData, backupFileName);
            File.Copy(SettingsFilePath, backupFilePath);
        }
        catch (Exception ex)
        {
            if (SharedLog.IsTraceEnabled)
                SharedLog.Trace($"Unable to create backup for {_fileName}", ex);
        }
    }
}
