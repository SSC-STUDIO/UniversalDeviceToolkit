using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Shared.Settings;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class AbstractSettingsPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronizeStore_BeforeFirstRead_ShouldPreserveExistingSettings(bool asynchronous)
    {
        using var fixture = new SettingsFixture();
        File.WriteAllText(fixture.FilePath, """{"Value":"saved-by-user"}""");
        var settings = new TestSettings(fixture.FilePath);

        if (asynchronous)
            await settings.SynchronizeStoreAsync();
        else
            settings.SynchronizeStore();

        fixture.ReadValue().Should().Be("saved-by-user");
        settings.Store.Value.Should().Be("saved-by-user");
    }

    [Fact]
    public async Task SynchronizeStoreAsync_WhenSavesOverlap_ShouldPersistLatestValue()
    {
        using var fixture = new SettingsFixture();
        using var settings = new BlockingSettings(fixture.FilePath);
        settings.Store.Value = "first";
        settings.PauseNextPathAccess();
        var first = Task.Run(settings.SynchronizeStoreAsync);
        Task? second = null;

        try
        {
            settings.WaitUntilPaused();
            settings.Store.Value = "latest";
            second = settings.SynchronizeStoreAsync();

            settings.PathReadsWhilePaused.Should().Be(0, "a later save must wait for the pending write");
        }
        finally
        {
            settings.Resume();
            await first;
            if (second is not null)
                await second;
        }

        fixture.ReadValue().Should().Be("latest");
        Directory.GetFiles(fixture.Directory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task SynchronizeStoreAsync_WhenWriteFails_ShouldAllowLaterSave()
    {
        using var fixture = new SettingsFixture();
        var settings = new TestSettings(fixture.FilePath);
        settings.Store.Value = "latest";
        Directory.CreateDirectory(fixture.FilePath);

        var exception = await Record.ExceptionAsync(settings.SynchronizeStoreAsync);
        (exception is IOException or UnauthorizedAccessException).Should().BeTrue(
            "failed replacement must surface an IO or access error");

        Directory.Delete(fixture.FilePath);
        await settings.SynchronizeStoreAsync();

        fixture.ReadValue().Should().Be("latest");
        Directory.GetFiles(fixture.Directory, "*.tmp").Should().BeEmpty();
    }

    private class TestSettings(string filePath) : AbstractSettings<SettingsStore>("settings.json")
    {
        protected override string SettingsFilePath => filePath;
    }

    private sealed class BlockingSettings(string filePath) : TestSettings(filePath), IDisposable
    {
        private readonly ManualResetEventSlim _paused = new();
        private readonly ManualResetEventSlim _resume = new();
        private int _pauseNextPathAccess;
        private int _pathReadsWhilePaused;
        private int _isPaused;

        public int PathReadsWhilePaused => Volatile.Read(ref _pathReadsWhilePaused);

        protected override string SettingsFilePath
        {
            get
            {
                if (Interlocked.Exchange(ref _pauseNextPathAccess, 0) == 1)
                {
                    Volatile.Write(ref _isPaused, 1);
                    _paused.Set();
                    if (!_resume.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Settings write did not resume.");
                    Volatile.Write(ref _isPaused, 0);
                }
                else if (Volatile.Read(ref _isPaused) == 1)
                {
                    Interlocked.Increment(ref _pathReadsWhilePaused);
                }

                return base.SettingsFilePath;
            }
        }

        public void PauseNextPathAccess() => Volatile.Write(ref _pauseNextPathAccess, 1);
        public void WaitUntilPaused() => _paused.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        public void Resume() => _resume.Set();

        public void Dispose()
        {
            _paused.Dispose();
            _resume.Dispose();
        }
    }

    public sealed class SettingsStore
    {
        public string Value { get; set; } = "default";
    }

    private sealed class SettingsFixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "udt-settings-" + Guid.NewGuid().ToString("N"));
        public string FilePath => Path.Combine(Directory, "settings.json");

        public SettingsFixture() => System.IO.Directory.CreateDirectory(Directory);

        public string? ReadValue()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            return document.RootElement.GetProperty("Value").GetString();
        }

        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
