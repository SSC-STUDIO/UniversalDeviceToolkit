using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public sealed class ManagementEventWatcherExtensionsSourceTests
{
    [Fact]
    public void StartWithTimeout_ShouldBePresent()
    {
        var source = ReadSourceFile();
        source.Should().Contain("public static void StartWithTimeout(");
    }

    [Fact]
    public void StartAsyncWithTimeout_ShouldBePresent()
    {
        var source = ReadSourceFile();
        source.Should().Contain("public static async Task StartAsyncWithTimeout(");
    }

    [Fact]
    public void StartWithTimeout_ShouldNotUseGetAwaiterGetResult()
    {
        var source = ReadSourceFile();
        var startMethod = ExtractMethod(source, "public static void StartWithTimeout(");
        startMethod.Should().NotContain("GetAwaiter().GetResult()");
    }

    [Fact]
    public void StartWithTimeout_ShouldDocumentWaitIsForNonUiThreads()
    {
        var source = ReadSourceFile();
        source.Should().Contain("Do not call from the UI thread");
        source.Should().Contain("startTask.Wait(timeoutMs)");
        source.Should().Contain("intentional for sync callers");
    }

    [Fact]
    public void StartAsyncWithTimeout_ShouldUseConfigureAwaitFalse()
    {
        var source = ReadSourceFile();
        source.Should().Contain("ConfigureAwait(false)");
        source.Should().Contain("Task.WhenAny(startTask, Task.Delay(timeoutMs, cts.Token))");
    }

    [Fact]
    public void WmiListenAsync_ShouldUseStartAsyncWithTimeout()
    {
        var source = ReadWmiSourceFile();
        source.Should().Contain("private static async Task<IDisposable> ListenAsync(");
        source.Should().Contain("await watcher.StartAsyncWithTimeout().ConfigureAwait(false)");
    }

    [Fact]
    public void WmiWrapper_ShouldExposeSubscribeAsyncUsingStartAsyncWithTimeout()
    {
        var source = ReadWmiWrapperSourceFile();
        source.Should().Contain("public async Task<IDisposable> SubscribeAsync(");
        source.Should().Contain("await watcher.StartAsyncWithTimeout().ConfigureAwait(false)");
        source.Should().Contain("Prefer");
        source.Should().Contain("SubscribeAsync");
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return string.Empty;

        var braceStart = source.IndexOf('{', start);
        if (braceStart < 0) return string.Empty;

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}') { depth--; if (depth == 0) return source[start..(i + 1)]; }
        }
        return string.Empty;
    }

    private static string ReadSourceFile() =>
        ReadRepositoryFile(Path.Combine("Libraries/Device", "Extensions", "ManagementEventWatcherExtensions.cs"));

    private static string ReadWmiSourceFile() =>
        ReadRepositoryFile(Path.Combine("Libraries/Device", "System", "Management", "WMI.cs"));

    private static string ReadWmiWrapperSourceFile() =>
        ReadRepositoryFile(Path.Combine("Libraries/Device", "System", "Management", "WMIWrapper.cs"));

    private static string ReadRepositoryFile(string relativePath)
    {
        foreach (var candidateRoot in GetRepositoryRootCandidates())
        {
            var path = Path.Combine(candidateRoot, relativePath);
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException($"Could not locate source file '{relativePath}'.");
    }

    private static IEnumerable<string> GetRepositoryRootCandidates()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("UDT_REPOSITORY_ROOT"),
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        };

        foreach (var root in roots.Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            var directory = new DirectoryInfo(root!);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "UniversalDeviceToolkit.sln")))
                    yield return directory.FullName;

                directory = directory.Parent;
            }
        }
    }
}
