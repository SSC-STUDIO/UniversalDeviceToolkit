using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Platform.Linux.Platform;

/// <summary>
/// Linux implementation of <see cref="IConfigurationStore"/>.
/// Persists configuration as a JSON file at <c>~/.local/share/udt/config.json</c>.
/// </summary>
public sealed class LinuxConfigurationStore : IConfigurationStore
{
    private readonly string _configDir;
    private readonly string _configFile;
    private readonly object _syncLock = new();
    private Dictionary<string, Dictionary<string, string>> _data;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxConfigurationStore"/> class
    /// using the default user-local configuration path.
    /// </summary>
    public LinuxConfigurationStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "udt",
            "config.json"))
    {
    }

    /// <summary>
    /// Initializes a new instance that reads and writes <paramref name="configFile"/>.
    /// </summary>
    /// <param name="configFile">Absolute or relative path to the JSON configuration file.</param>
    public LinuxConfigurationStore(string configFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configFile);

        _configFile = Path.GetFullPath(configFile);
        _configDir = Path.GetDirectoryName(_configFile)
            ?? throw new ArgumentException("Configuration path must include a file name.", nameof(configFile));
        _data = LoadData();
    }

    /// <inheritdoc />
    public string? GetValue(string section, string key)
    {
        lock (_syncLock)
        {
            return _data.TryGetValue(section, out var sectionData)
                && sectionData.TryGetValue(key, out var value)
                ? value
                : null;
        }
    }

    /// <inheritdoc />
    public void SetValue(string section, string key, string? value)
    {
        lock (_syncLock)
        {
            var next = CloneData(_data);
            if (!next.TryGetValue(section, out var sectionData))
            {
                sectionData = new Dictionary<string, string>();
                next[section] = sectionData;
            }

            if (value is null)
                sectionData.Remove(key);
            else
                sectionData[key] = value;

            if (sectionData.Count == 0)
                next.Remove(section);

            SaveData(next);
            _data = next;
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetSection(string section)
    {
        lock (_syncLock)
        {
            return _data.TryGetValue(section, out var sectionData)
                ? new Dictionary<string, string>(sectionData)
                : new Dictionary<string, string>();
        }
    }

    private Dictionary<string, Dictionary<string, string>> LoadData()
    {
        if (!File.Exists(_configFile))
            return new Dictionary<string, Dictionary<string, string>>();

        try
        {
            var json = File.ReadAllText(_configFile);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, JsonOptions);
            if (parsed is not null)
                return parsed;
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        TryBackupTornFile();
        return new Dictionary<string, Dictionary<string, string>>();
    }

    private void SaveData(Dictionary<string, Dictionary<string, string>> data)
    {
        Directory.CreateDirectory(_configDir);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        AtomicWriteAllText(_configFile, json);
    }

    private void TryBackupTornFile()
    {
        try
        {
            if (!File.Exists(_configFile))
                return;

            var backupName = $"{Path.GetFileName(_configFile)}.torn-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Copy(_configFile, Path.Combine(_configDir, backupName), overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Dictionary<string, Dictionary<string, string>> CloneData(
        Dictionary<string, Dictionary<string, string>> source)
    {
        var clone = new Dictionary<string, Dictionary<string, string>>(source.Count, source.Comparer);
        foreach (var (section, values) in source)
            clone[section] = new Dictionary<string, string>(values, values.Comparer);
        return clone;
    }

    /// <summary>
    /// Write via temp + replace so a crash mid-write cannot leave a partial JSON file.
    /// </summary>
    private static void AtomicWriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
