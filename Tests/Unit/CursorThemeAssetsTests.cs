using System;
using System.IO;
using UniversalDeviceToolkit.Lib.Features.CursorPointer;
using Xunit;

namespace UniversalDeviceToolkit.Tests;

public sealed class CursorThemeAssetsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "UDT.CursorAssets." + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Persist_CursorsSurviveRemovalOfSourceInstallation()
    {
        var source = Path.Combine(_root, "installation");
        var animations = Path.Combine(source, "animations");
        Directory.CreateDirectory(animations);
        File.WriteAllBytes(Path.Combine(source, "Pointer.cur"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(animations, "Busy.ani"), [4, 5, 6]);
        var (basePath, animationPath) = CursorThemeAssets.Persist(source, animations,
            Path.Combine(_root, "user-data"), ["Pointer.cur", "Busy.ani"]);

        Directory.Delete(source, recursive: true);

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(basePath, "Pointer.cur")));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(animationPath, "Busy.ani")));
    }

    [Fact]
    public void Persist_IncompleteSourceLeavesActiveAssetsIntact()
    {
        var source = Path.Combine(_root, "installation");
        var destination = Path.Combine(_root, "user-data");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        File.WriteAllBytes(Path.Combine(source, "Pointer.cur"), [2]);
        File.WriteAllBytes(Path.Combine(destination, "Pointer.cur"), [1]);

        Assert.Throws<FileNotFoundException>(() => CursorThemeAssets.Persist(source, source, destination,
            ["Pointer.cur", "Busy.ani"]));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(destination, "Pointer.cur")));
    }

    [Fact]
    public void Persist_UpdatesExistingAssetsWithoutLeavingTemporaryFiles()
    {
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "user-data");
        var sourceFile = Path.Combine(_root, "Pointer.cur");
        File.WriteAllBytes(sourceFile, [1]);
        CursorThemeAssets.Persist(_root, _root, destination, ["Pointer.cur"]);
        File.WriteAllBytes(sourceFile, [2]);
        CursorThemeAssets.Persist(_root, _root, destination, ["Pointer.cur"]);

        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(destination, "Pointer.cur")));
        Assert.Single(Directory.GetFiles(destination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
