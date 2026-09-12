using System;
using System.IO;
using System.Security.Cryptography;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Security)]
public sealed class InstallerLaunchPathValidatorTests : IDisposable
{
    private readonly string _root;

    public InstallerLaunchPathValidatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"udt-installer-launch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void TryValidateForExecution_WhenPathNameAndHashMatch_ShouldSucceed()
    {
        var (filePath, fileName, hash) = WriteInstaller("payload-ok");

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            filePath, _root, fileName, hash, out var normalized, out var failureReason);

        ok.Should().BeTrue(failureReason);
        normalized.Should().Be(Path.GetFullPath(filePath));
        failureReason.Should().BeEmpty();
    }

    [Fact]
    public void TryValidateForExecution_WhenHashCasingDiffers_ShouldSucceed()
    {
        var (filePath, fileName, hash) = WriteInstaller("payload-case");

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            filePath, _root, fileName, hash.ToLowerInvariant(), out _, out var failureReason);

        ok.Should().BeTrue(failureReason);
    }

    [Fact]
    public void TryValidateForExecution_WhenHashMismatches_ShouldFail()
    {
        var (filePath, fileName, _) = WriteInstaller("payload-tampered");
        var otherHash = Convert.ToHexString(SHA256.HashData("other"u8.ToArray()));

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            filePath, _root, fileName, otherHash, out var normalized, out var failureReason);

        ok.Should().BeFalse();
        normalized.Should().BeEmpty();
        failureReason.Should().Be("Installer checksum mismatch.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeX")]
    public void TryValidateForExecution_WhenExpectedHashIsInvalid_ShouldFail(string? hash)
    {
        var (filePath, fileName, _) = WriteInstaller("payload-invalid-hash");

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            filePath, _root, fileName, hash, out var normalized, out var failureReason);

        ok.Should().BeFalse();
        normalized.Should().BeEmpty();
        failureReason.Should().Be("Installer checksum is missing or invalid.");
    }

    [Fact]
    public void TryValidateForExecution_WhenFileIsOutsideDownloadDirectory_ShouldFail()
    {
        var outsideRoot = Path.Combine(Path.GetTempPath(), $"udt-installer-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideRoot);
        try
        {
            var fileName = "outside.exe";
            var filePath = Path.Combine(outsideRoot, fileName);
            File.WriteAllText(filePath, "outside");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));

            var ok = InstallerLaunchPathValidator.TryValidateForExecution(
                filePath, _root, fileName, hash, out var normalized, out var failureReason);

            ok.Should().BeFalse();
            normalized.Should().BeEmpty();
            failureReason.Should().Be("Installer path is outside the configured download directory.");
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void TryValidateForExecution_WhenFileNameDiffers_ShouldFail()
    {
        var (filePath, _, hash) = WriteInstaller("payload-name");

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            filePath, _root, "other-name.exe", hash, out var normalized, out var failureReason);

        ok.Should().BeFalse();
        normalized.Should().BeEmpty();
        failureReason.Should().StartWith("Unexpected installer file name:");
    }

    [Fact]
    public void TryValidateForExecution_WhenFileIsMissing_ShouldFail()
    {
        var missing = Path.Combine(_root, "missing.exe");
        var hash = new string('a', InstallerLaunchPathValidator.Sha256HexLength);

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            missing, _root, "missing.exe", hash, out var normalized, out var failureReason);

        ok.Should().BeFalse();
        normalized.Should().BeEmpty();
        failureReason.Should().Be("Installer file does not exist.");
    }

    [Fact]
    public void TryValidateForExecution_WhenPathIsDirectory_ShouldFail()
    {
        var hash = new string('a', InstallerLaunchPathValidator.Sha256HexLength);

        var ok = InstallerLaunchPathValidator.TryValidateForExecution(
            _root, _root, Path.GetFileName(_root), hash, out var normalized, out var failureReason);

        ok.Should().BeFalse();
        normalized.Should().BeEmpty();
        failureReason.Should().BeOneOf("Installer file does not exist.", "Installer path points to a directory.");
    }

    [Fact]
    public void TryComputeSha256Hex_ShouldMatchHashData()
    {
        var (filePath, _, expected) = WriteInstaller("hash-helper");

        var ok = InstallerLaunchPathValidator.TryComputeSha256Hex(filePath, out var actual, out var failureReason);

        ok.Should().BeTrue(failureReason);
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSha256Hex_ShouldAcceptOnly64HexCharacters(string? value, bool expected)
    {
        InstallerLaunchPathValidator.IsSha256Hex(value).Should().Be(expected);
    }

    private (string FilePath, string FileName, string Sha256Hex) WriteInstaller(string payload)
    {
        var fileName = $"{payload}.exe";
        var filePath = Path.Combine(_root, fileName);
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        File.WriteAllBytes(filePath, bytes);
        return (filePath, fileName, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
