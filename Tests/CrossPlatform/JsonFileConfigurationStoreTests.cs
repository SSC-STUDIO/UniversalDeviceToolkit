using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Platform.Linux.Platform;
using UniversalDeviceToolkit.Platform.MacOS.Platform;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class JsonFileConfigurationStoreTests
{
    public static TheoryData<string> StoreKinds { get; } = new() { "linux", "macos" };

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void SetValue_ShouldPersistCompleteJsonAndReload(string storeKind)
    {
        using var temp = new TempConfig();
        var store = Create(storeKind, temp.FilePath);

        store.SetValue("power", "profile", "balanced");
        store.SetValue("display", "brightness", "80");

        store.GetValue("power", "profile").Should().Be("balanced");
        store.GetSection("display").Should().ContainKey("brightness").WhoseValue.Should().Be("80");

        var onDisk = File.ReadAllText(temp.FilePath);
        using var document = JsonDocument.Parse(onDisk);
        document.RootElement.GetProperty("power").GetProperty("profile").GetString().Should().Be("balanced");
        Directory.GetFiles(temp.Directory, "*.tmp").Should().BeEmpty();

        var reloaded = Create(storeKind, temp.FilePath);
        reloaded.GetValue("power", "profile").Should().Be("balanced");
        reloaded.GetValue("display", "brightness").Should().Be("80");
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void SetValue_WhenValueIsNull_ShouldRemoveKeyFromDisk(string storeKind)
    {
        using var temp = new TempConfig();
        var store = Create(storeKind, temp.FilePath);
        store.SetValue("power", "profile", "balanced");
        store.SetValue("power", "profile", null);

        store.GetValue("power", "profile").Should().BeNull();
        store.GetSection("power").Should().BeEmpty();

        var reloaded = Create(storeKind, temp.FilePath);
        reloaded.GetValue("power", "profile").Should().BeNull();
        reloaded.GetSection("power").Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void SetValue_WhenWriteFails_ShouldThrowAndKeepPreviousValue(string storeKind)
    {
        using var temp = new TempConfig();
        var store = Create(storeKind, temp.FilePath);
        store.SetValue("power", "profile", "balanced");

        File.Delete(temp.FilePath);
        Directory.CreateDirectory(temp.FilePath);

        var act = () => store.SetValue("power", "profile", "performance");

        var exception = act.Should().Throw<Exception>().Which;
        (exception is IOException || exception is UnauthorizedAccessException).Should().BeTrue(
            "write failure must surface as IOException or UnauthorizedAccessException, but was {0}",
            exception.GetType().FullName);
        store.GetValue("power", "profile").Should().Be("balanced");
        Directory.GetFiles(temp.Directory, "*.tmp").Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void SetValue_WhenDirectoryCannotBeCreated_ShouldThrowWithoutPublishingValue(string storeKind)
    {
        using var temp = new TempConfig();
        var blocker = Path.Combine(temp.Directory, "not-a-directory");
        File.WriteAllText(blocker, "x");
        var store = Create(storeKind, Path.Combine(blocker, "config.json"));

        var act = () => store.SetValue("power", "profile", "balanced");

        act.Should().Throw<IOException>();
        store.GetValue("power", "profile").Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public void Load_WhenJsonIsTorn_ShouldBackupAndStartEmptyThenWriteValidJson(string storeKind)
    {
        using var temp = new TempConfig();
        File.WriteAllText(temp.FilePath, """{ "power": { "profile": """);

        var store = Create(storeKind, temp.FilePath);

        store.GetValue("power", "profile").Should().BeNull();
        Directory.GetFiles(temp.Directory, "config.json.torn-*").Should().NotBeEmpty();

        store.SetValue("power", "profile", "balanced");

        using var document = JsonDocument.Parse(File.ReadAllText(temp.FilePath));
        document.RootElement.GetProperty("power").GetProperty("profile").GetString().Should().Be("balanced");
        Directory.GetFiles(temp.Directory, "*.tmp").Should().BeEmpty();
    }

    private static IConfigurationStore Create(string storeKind, string configFile) =>
        storeKind switch
        {
            "linux" => new LinuxConfigurationStore(configFile),
            "macos" => new MacOSConfigurationStore(configFile),
            _ => throw new ArgumentOutOfRangeException(nameof(storeKind), storeKind, "Unknown store kind.")
        };

    private sealed class TempConfig : IDisposable
    {
        public string Directory { get; }
        public string FilePath { get; }

        public TempConfig()
        {
            Directory = Path.Combine(Path.GetTempPath(), "udt-config-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            FilePath = Path.Combine(Directory, "config.json");
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                    System.IO.Directory.Delete(Directory, recursive: true);
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
