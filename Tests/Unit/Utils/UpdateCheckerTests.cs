using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using Octokit;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class UpdateCheckerTests : TemporaryFileTestBase
{
    private static readonly MethodInfo ValidateUpdatePackageAsyncMethod = typeof(UpdateChecker)
        .GetMethod("ValidateUpdatePackageAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void FilterPublicApplicationReleases_ShouldIgnoreRollingPluginCatalog()
    {
        // Arrange
        var catalogRelease = CreateRelease("plugin-catalog", string.Empty, prerelease: false);
        var previewCatalogRelease = CreateRelease("plugin-catalog-preview", string.Empty, prerelease: true);
        var applicationRelease = CreateRelease("v5.0.1", string.Empty, prerelease: false);
        var previewRelease = CreateRelease("v5.1.0-rc.1", string.Empty, prerelease: true);

        // Act
        var stableReleases = UpdateChecker.FilterPublicApplicationReleases(
            [catalogRelease, previewCatalogRelease, applicationRelease, previewRelease],
            includePrerelease: false);
        var previewReleases = UpdateChecker.FilterPublicApplicationReleases(
            [catalogRelease, previewCatalogRelease, applicationRelease, previewRelease],
            includePrerelease: true);

        // Assert
        stableReleases.Should().ContainSingle().Which.TagName.Should().Be("v5.0.1");
        previewReleases.Should().HaveCount(2);
        previewReleases.Should().NotContain(release => ReleaseTagPolicy.IsCatalogTag(release.TagName));
    }

    [Fact]
    public void Update_ShouldRecognizeCurrentSha256TextAsset()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkit_v2.14.0_Setup.exe";
        const string sha256Url = "https://example.com/UniversalDeviceToolkit_v2.14.0_SHA256.txt";
        var release = CreateRelease(
            body: string.Empty,
            ("UniversalDeviceToolkit_v2.14.0_Setup.exe", packageUrl),
            ("UniversalDeviceToolkit_v2.14.0_SHA256.txt", sha256Url));

        // Act
        var update = new Update(release);

        // Assert
        update.Url.Should().Be(packageUrl);
        update.Sha256Url.Should().Be(sha256Url);
        update.Sha256Hash.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldPreferChosenInstallerHashOverOtherPackagesAndManifest()
    {
        const string setup = "UniversalDeviceToolkit_v6.1.4_Full_Setup.exe";
        const string setupUrl = "https://example.com/full.exe";
        const string hashUrl = "https://example.com/full.sha256";
        var update = new Update(CreateRelease(string.Empty,
            ("UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe.sha256", "https://example.com/compatibility.sha256"),
            ("UniversalDeviceToolkit_v6.1.4_SHA256.txt", "https://example.com/all.txt"),
            (setup, setupUrl),
            (setup.ToUpperInvariant() + ".SHA256", hashUrl)));

        update.Url.Should().Be(setupUrl);
        update.Sha256Url.Should().Be(hashUrl);
    }

    [Fact]
    public void Update_ShouldIgnoreUnrelatedIndividualHashAndUseSharedManifest()
    {
        const string manifestUrl = "https://example.com/all.txt";
        var update = new Update(CreateRelease(string.Empty,
            ("UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe.sha256", "https://example.com/compatibility.sha256"),
            ("UniversalDeviceToolkit_v6.1.4_Full_Setup.exe", "https://example.com/full.exe"),
            ("UniversalDeviceToolkit_v6.1.4_SHA256.txt", manifestUrl)));

        update.Sha256Url.Should().Be(manifestUrl);
    }

    [Fact]
    public void Update_ShouldNotUseAnotherInstallersIndividualHash()
    {
        var update = new Update(CreateRelease(string.Empty,
            ("UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe.sha256", "https://example.com/compatibility.sha256"),
            ("UniversalDeviceToolkit_v6.1.4_Full_Setup.exe", "https://example.com/full.exe")));

        update.Sha256Url.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldReadReleaseBodyHashOnlyForSelectedInstaller()
    {
        const string online = "UniversalDeviceToolkit_v6.1.4_Online_Setup.exe";
        const string full = "UniversalDeviceToolkit_v6.1.4_Full_Setup.exe";
        var onlineHash = new string('b', 64);
        var fullHash = new string('a', 64);
        var update = new Update(CreateRelease($"{online}: {onlineHash}\n{full}: {fullHash}",
            (online, "https://example.com/online.exe"),
            (full, "https://example.com/full.exe")));

        update.Sha256Hash.Should().Be(fullHash);
    }

    [Fact]
    public void Update_ShouldRejectReleaseBodyHashForDifferentInstaller()
    {
        var update = new Update(CreateRelease($"UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe: {new string('b', 64)}",
            ("UniversalDeviceToolkit_v6.1.4_Full_Setup.exe", "https://example.com/full.exe")));

        update.Sha256Hash.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldPreferFullInstallerWhenEnglishOnlyInstallerIsPresent()
    {
        // Arrange
        const string englishPackageUrl = "https://example.com/UniversalDeviceToolkit_v3.7.0_English_Setup.exe";
        const string fullPackageUrl = "https://example.com/UniversalDeviceToolkit_v3.7.0_Setup.exe";
        var release = CreateRelease(
            body: string.Empty,
            ("UniversalDeviceToolkit_v3.7.0_English_Setup.exe", englishPackageUrl),
            ("UniversalDeviceToolkit_v3.7.0_Setup.exe", fullPackageUrl));

        // Act
        var update = new Update(release);

        // Assert
        update.Url.Should().Be(fullPackageUrl);
    }

    [Fact]
    public void Update_ShouldPreferUniversalFullInstallerOverOnlineAndLegacyAlias()
    {
        // Arrange
        const string onlinePackageUrl = "https://example.com/UniversalDeviceToolkit_v3.8.0_Online_Setup.exe";
        const string legacyPackageUrl = "https://example.com/UniversalDeviceToolkit_v3.8.0_Setup.exe";
        const string fullPackageUrl = "https://example.com/UniversalDeviceToolkit_v3.8.0_Full_Setup.exe";
        var release = CreateRelease(
            body: string.Empty,
            ("UniversalDeviceToolkit_v3.8.0_Online_Setup.exe", onlinePackageUrl),
            ("UniversalDeviceToolkit_v3.8.0_Setup.exe", legacyPackageUrl),
            ("UniversalDeviceToolkit_v3.8.0_Full_Setup.exe", fullPackageUrl));

        // Act
        var update = new Update(release);

        // Assert
        update.Url.Should().Be(fullPackageUrl);
    }

    [Fact]
    public void Update_ShouldParsePackageSpecificHashFromReleaseBody()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkit_v2.14.0_Setup.exe";
        var zipHash = new string('1', 64);
        var setupHash = new string('a', 64);
        var release = CreateRelease(
            body: $"""
                   ## Verification
                   UniversalDeviceToolkit_v2.14.0_win-x64.zip: {zipHash}
                   UniversalDeviceToolkit_v2.14.0_Setup.exe: {setupHash}
                   """,
            ("UniversalDeviceToolkit_v2.14.0_Setup.exe", packageUrl));

        // Act
        var update = new Update(release);

        // Assert
        update.Sha256Hash.Should().Be(setupHash);
    }

    [Fact]
    public async Task ValidateUpdatePackageAsync_WhenSha256TxtContainsMultipleEntries_ShouldUsePackageSpecificHash()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkit_v2.14.0_Setup.exe";
        const string sha256Url = "https://example.com/UniversalDeviceToolkit_v2.14.0_SHA256.txt";
        var packageBytes = "signed update payload"u8.ToArray();
        var expectedHash = ComputeSha256(packageBytes);
        var tempFile = CreateTempFile();
        await File.WriteAllBytesAsync(tempFile, packageBytes);

        var update = new Update(CreateRelease(
            body: string.Empty,
            ("UniversalDeviceToolkit_v2.14.0_Setup.exe", packageUrl),
            ("UniversalDeviceToolkit_v2.14.0_SHA256.txt", sha256Url)));

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            request.RequestUri.Should().NotBeNull();
            request.RequestUri!.ToString().Should().Be(sha256Url);

            var sha256List = $"""
                              {new string('b', 64)} UniversalDeviceToolkit_v2.14.0_win-x64.zip
                              SHA256 (UniversalDeviceToolkit_v2.14.0_Setup.exe) = {expectedHash}
                              """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sha256List)
            };
        }));

        // Act
        await InvokeValidateUpdatePackageAsync(tempFile, update, httpClient);

        // Assert
        File.Exists(tempFile).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateUpdatePackageAsync_WhenLegacySha256AssetContainsRawHash_ShouldAcceptPackage()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkitSetup.exe";
        const string sha256Url = "https://example.com/UniversalDeviceToolkitSetup.exe.sha256";
        var packageBytes = "legacy update payload"u8.ToArray();
        var expectedHash = ComputeSha256(packageBytes);
        var tempFile = CreateTempFile();
        await File.WriteAllBytesAsync(tempFile, packageBytes);

        var update = new Update(CreateRelease(
            body: string.Empty,
            ("UniversalDeviceToolkitSetup.exe", packageUrl),
            ("UniversalDeviceToolkitSetup.exe.sha256", sha256Url)));

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(expectedHash)
        }));

        // Act
        await InvokeValidateUpdatePackageAsync(tempFile, update, httpClient);

        // Assert
        File.Exists(tempFile).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateUpdatePackageAsync_WhenBodyHashDoesNotMatch_ShouldDeleteFileAndThrow()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkitSetup.exe";
        var tempFile = CreateTempFile();
        await File.WriteAllBytesAsync(tempFile, "tampered payload"u8.ToArray());

        var update = new Update(CreateRelease(
            body: $"SHA256: {new string('c', 64)}",
            ("UniversalDeviceToolkitSetup.exe", packageUrl)));

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("unexpected request")));

        // Act
        var action = () => InvokeValidateUpdatePackageAsync(tempFile, update, httpClient);

        // Assert
        await action.Should().ThrowAsync<InvalidDataException>();
        File.Exists(tempFile).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateUpdatePackageAsync_WhenNoHashIsAvailable_ShouldSkipValidation()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkitSetup.exe";
        var tempFile = CreateTempFile();
       await File.WriteAllBytesAsync(tempFile, "unsigned payload"u8.ToArray());

       var update = new Update(CreateRelease(
           body: string.Empty,
           ("UniversalDeviceToolkitSetup.exe", packageUrl)));

       using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("unexpected request")));

       // Act
        // The test name documents the skip path. An explicit integrity-check waiver makes the
        // skip path run consistently in both Debug and Release, rather than relying on the
        // #if !DEBUG security gate being compiled out under DEBUG.
        await InvokeValidateUpdatePackageAsync(tempFile, update, httpClient, isIntegrityCheckRequired: false);

       // Assert
       File.Exists(tempFile).Should().BeTrue();
   }

    [Fact]
    public async Task ValidateUpdatePackageAsync_WhenNoHashIsAvailableAndIntegrityRequired_EnforcesReleaseSecurityGate()
    {
        // Arrange
        const string packageUrl = "https://example.com/UniversalDeviceToolkitSetup.exe";
        var tempFile = CreateTempFile();
        await File.WriteAllBytesAsync(tempFile, "unverified payload"u8.ToArray());

        var update = new Update(CreateRelease(
            body: string.Empty,
            ("UniversalDeviceToolkitSetup.exe", packageUrl)));

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("unexpected request")));

        // The production security gate (UpdateChecker.ValidateUpdatePackageAsync, guarded by
        // #if !DEBUG) only throws when integrity is required and no hash resolves. Debug compiles
        // the throw out, so the same invocation skips validation and keeps the file; Release enforces it.
        var action = () => InvokeValidateUpdatePackageAsync(tempFile, update, httpClient, isIntegrityCheckRequired: true);
#if DEBUG
        await action();
        File.Exists(tempFile).Should().BeTrue();
#else
        await action.Should().ThrowAsync<InvalidDataException>();
        File.Exists(tempFile).Should().BeFalse();
#endif
    }

   private static async Task InvokeValidateUpdatePackageAsync(string filePath, Update update, HttpClient httpClient)
        => await InvokeValidateUpdatePackageAsync(filePath, update, httpClient, isIntegrityCheckRequired: true);

    private static async Task InvokeValidateUpdatePackageAsync(string filePath, Update update, HttpClient httpClient, bool isIntegrityCheckRequired)
    {
        var task = (Task)ValidateUpdatePackageAsyncMethod.Invoke(null, [filePath, update, httpClient, CancellationToken.None, isIntegrityCheckRequired])!;
        await task;
    }

    private static string ComputeSha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Release CreateRelease(string body, params (string Name, string Url)[] assets)
        => CreateRelease("v2.14.0", body, prerelease: false, assets);

    private static Release CreateRelease(
        string tagName,
        string body,
        bool prerelease,
        params (string Name, string Url)[] assets)
    {
        var release = CreateUninitialized<Release>();
        SetAutoProperty(release, nameof(Release.TagName), tagName);
        SetAutoProperty(release, nameof(Release.Name), $"Universal Device Toolkit {tagName}");
        SetAutoProperty(release, nameof(Release.Body), body);
        SetAutoProperty(release, nameof(Release.Prerelease), prerelease);
        SetAutoProperty(release, nameof(Release.Draft), false);
        SetAutoProperty(release, nameof(Release.CreatedAt), DateTimeOffset.UtcNow);
        SetAutoProperty(release, nameof(Release.PublishedAt), DateTimeOffset.UtcNow);
        SetAutoProperty(release, nameof(Release.Assets), Array.ConvertAll(assets, asset => CreateReleaseAsset(asset.Name, asset.Url)));
        return release;
    }

    private static ReleaseAsset CreateReleaseAsset(string name, string url)
    {
        var asset = CreateUninitialized<ReleaseAsset>();
        SetAutoProperty(asset, nameof(ReleaseAsset.Name), name);
        SetAutoProperty(asset, nameof(ReleaseAsset.BrowserDownloadUrl), url);
        return asset;
    }

    private static T CreateUninitialized<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void SetAutoProperty<T>(object target, string propertyName, T value)
    {
        var field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{target.GetType().Name}.{propertyName} should exist");
        field!.SetValue(target, value);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
