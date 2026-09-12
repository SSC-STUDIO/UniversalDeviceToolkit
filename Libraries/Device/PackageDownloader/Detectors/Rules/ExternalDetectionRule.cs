using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal readonly struct ExternalDetectionRule : IPackageRule
{
    private const string TEMP_FOLDER_SUB_FOLDER = "external_package_detection";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe"
    };

    private static readonly HashSet<string> AllowedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "download.lenovo.com",
        "downloads.lenovo.com"
    };

    private int[] ReturnCodes { get; init; }
    private string Command { get; init; }
    private string Url { get; init; }
    private string FileName { get; init; }
    private string PackageName { get; init; }
    private string? ExpectedSha256 { get; init; }

    public static bool TryCreate(XmlNode? node, XmlDocument document, string baseLocation, out ExternalDetectionRule value)
    {
        var command = node?.InnerText;
        var returnCodes = node?.Attributes?.OfType<XmlAttribute>()
            .FirstOrDefault(a => a.Name == "rc")?
            .InnerText
            .Split(",")
            .Select(s => int.TryParse(s, out var result) ? result : -1)
            .Where(i => i >= 0)
            .Distinct()
            .ToArray() ?? [];
        var externalFile = document.SelectSingleNode("/Package/Files/External/File/Name")?.InnerText;
        var packageName = document.SelectSingleNode("/Package/@id")?.InnerText;
        var externalCrc = document.SelectSingleNode("/Package/Files/External/File/CRC")?.InnerText;

        if (command is null
            || returnCodes.IsEmpty()
            || externalFile is null
            || packageName is null
            || !PathSecurity.IsValidFileName(externalFile)
            || !PathSecurity.IsValidFileName(packageName)
            || !PackageDownloadSecurity.TryParseSha256Hex(externalCrc, out _)
            || !PackageDownloadSecurity.IsAllowedPackageDownloadUrl($"{baseLocation}/{externalFile}"))
        {
            value = default;
            return false;
        }

        var expectedSha256 = externalCrc!.Trim();

        value = new ExternalDetectionRule
        {
            Command = command,
            ReturnCodes = returnCodes,
            Url = $"{baseLocation}/{externalFile}",
            FileName = externalFile,
            PackageName = packageName,
            ExpectedSha256 = expectedSha256
        };
        return true;
    }

    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _, HttpClient httpClient, CancellationToken token) => CheckExternalDependency(httpClient, token);

    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _, HttpClient httpClient, CancellationToken token) => CheckExternalDependency(httpClient, token);

    private async Task<bool> CheckExternalDependency(HttpClient httpClient, CancellationToken token)
    {
        var detectionRoot = Path.Combine(Folders.Temp, TEMP_FOLDER_SUB_FOLDER);
        Directory.CreateDirectory(detectionRoot);

        var packagePath = Path.Combine(detectionRoot, PathSecurity.SanitizeFileName(PackageName));
        if (!PathSecurity.IsPathWithinAllowedDirectory(packagePath, detectionRoot))
            return false;

        Directory.CreateDirectory(packagePath);

        var filePath = PathSecurity.CreateSafeFilePath(packagePath, FileName);
        if (filePath is null)
            return false;

        if (!File.Exists(filePath))
        {
            var extension = Path.GetExtension(FileName);
            if (!AllowedExtensions.Contains(extension ?? string.Empty))
            {
                Log.Instance.Warning($"Rejecting download of '{FileName}' from '{Url}': extension '{extension}' is not in the allowed list");
                return false;
            }

            if (!IsAllowedDownloadUrl(Url))
            {
                Log.Instance.Warning($"Rejecting download of '{FileName}' from '{Url}': host is not in the allowed list");
                return false;
            }

            await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                await httpClient.DownloadAsync(Url, fileStream, null, token).ConfigureAwait(false);

            if (!await ValidateDownloadedFileAsync(filePath, token).ConfigureAwait(false))
            {
                try { File.Delete(filePath); } catch (IOException) { }
                return false;
            }
        }
        else if (!await ValidateDownloadedFileAsync(filePath, token).ConfigureAwait(false))
        {
            return false;
        }

        var parsed = ParseCommand(Command, packagePath);
        var executable = parsed.Executable;
        var arguments = parsed.Arguments;

        if (string.IsNullOrWhiteSpace(executable))
            return false;

        string resolvedExecutable;
        try
        {
            resolvedExecutable = Path.GetFullPath(executable);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!PathSecurity.IsPathWithinAllowedDirectory(resolvedExecutable, packagePath))
        {
            Log.Instance.Warning($"Rejecting executable '{resolvedExecutable}': path escapes package directory '{packagePath}'");
            return false;
        }

        var executableExtension = Path.GetExtension(resolvedExecutable);
        if (!AllowedExtensions.Contains(executableExtension ?? string.Empty))
        {
            Log.Instance.Warning($"Rejecting executable '{resolvedExecutable}': extension '{executableExtension}' is not in the allowed list");
            return false;
        }

        if (!File.Exists(resolvedExecutable))
        {
            Log.Instance.Warning($"Rejecting executable '{resolvedExecutable}': file does not exist");
            return false;
        }

        Log.Instance.Warning($"Executing external detection command: '{executable}' with args: '{arguments}' for package '{PackageName}'");

        var (exitCode, _) = await CMD.RunAsync(executable, arguments, token: token).ConfigureAwait(false);
        var result = ReturnCodes.Contains(exitCode);
        return result;
    }

    private async Task<bool> ValidateDownloadedFileAsync(string filePath, CancellationToken token)
    {
        if (string.IsNullOrEmpty(ExpectedSha256))
        {
            Log.Instance.Warning($"Rejecting external detection file '{FileName}': catalog SHA-256 is missing");
            return false;
        }

        await using var fileStream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(await sha256.ComputeHashAsync(fileStream, token).ConfigureAwait(false));
        if (PackageDownloadSecurity.Sha256Equals(hash, ExpectedSha256))
            return true;

        Log.Instance.Warning($"Rejecting external detection file '{FileName}': catalog SHA-256 mismatch");
        return false;
    }

    private static bool IsAllowedDownloadUrl(string url)
    {
        if (!PackageDownloadSecurity.IsAllowedPackageDownloadUrl(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return AllowedDownloadHosts.Contains(uri.Host);
    }

    private static (string Executable, string Arguments) ParseCommand(string command, string packagePath)
    {
        if (string.IsNullOrWhiteSpace(command))
            return (string.Empty, string.Empty);

        var substituted = command.Replace("%PACKAGEPATH%", packagePath);

        var parts = substituted.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (string.Empty, string.Empty);

        var executableToken = parts[0];
        var executable = executableToken;

        if (!executable.Contains('\\') && !executable.Contains('/'))
            executable = Path.Combine(packagePath, executable);

        var arguments = parts.Length > 1 ? string.Join(' ', parts, 1, parts.Length - 1) : string.Empty;

        return (executable, arguments);
    }
}
