using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class ShippingFootprintPruneTests
{
    [Theory]
    [InlineData("utf8", 0, 0)]
    [InlineData("utf8", 1, 0)]
    [InlineData("utf8", 37, 5)]
    [InlineData("utf16le", 0, 0)]
    [InlineData("utf16le", 1, 0)]
    [InlineData("utf16le", 37, 5)]
    [InlineData("utf16be", 0, 0)]
    [InlineData("utf16be", 1, 0)]
    [InlineData("utf16be", 37, 5)]
    public async Task AssertShippingPayload_RejectsBinaryMarkersAtAnyByteOffset(
        string encodingName, int prefixLength, int suffixLength)
    {
        var root = NewTempDirectory();
        try
        {
            var markerBytes = GetMarkerEncoding(encodingName).GetBytes("UDT_APPDATA_OVERRIDE");
            var bytes = new byte[prefixLength + markerBytes.Length + suffixLength];
            markerBytes.CopyTo(bytes, prefixLength);
            File.WriteAllBytes(Path.Combine(root, "payload.dll"), bytes);

            var result = await RunPayloadGuardAsync(root);

            result.ExitCode.Should().Be(1);
            result.Error.Should().Contain("Shipping payload contains test or validation tool artifacts:");
            result.Error.Should().Contain("payload.dll");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssertShippingPayload_AcceptsKnownRuntimeMarkerAndNonmatchingPayloads()
    {
        var root = NewTempDirectory();
        try
        {
            File.WriteAllBytes(
                Path.Combine(root, "UniversalDeviceToolkit.Lib.Abstractions.dll"),
                [.. Encoding.UTF8.GetBytes("UDT_APPDATA_OVERRIDE"),
                 .. Encoding.Unicode.GetBytes("UDT_APPDATA_OVERRIDE"),
                 .. Encoding.BigEndianUnicode.GetBytes("UDT_APPDATA_OVERRIDE")]);
            foreach (var encodingName in new[] { "utf8", "utf16le", "utf16be" })
            {
                File.WriteAllBytes(
                    Path.Combine(root, $"near-match-{encodingName}.dll"),
                    GetMarkerEncoding(encodingName).GetBytes(
                        "UDT_APPDATA_OVERRID XDT_APPDATA_OVERRIDE udt_appdata_override"));
            }
            File.WriteAllBytes(Path.Combine(root, "short.dll"), [0, 1, 2]);
            File.WriteAllBytes(Path.Combine(root, "empty.dll"), []);

            var result = await RunPayloadGuardAsync(root);

            result.ExitCode.Should().Be(0, "PowerShell output was: {0}{1}", result.Output, result.Error);
            result.Output.Should().Contain("Shipping payload validation passed:");
            result.Error.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("SpectrumTester.exe")]
    [InlineData("UniversalDeviceToolkit.Tests.dll")]
    [InlineData("payload.pdb")]
    [InlineData("nested/TeStS/payload.dll")]
    [InlineData("nested/Tools/UniversalDeviceToolkit.Lib.Abstractions.dll")]
    [InlineData("nested/x86/native.dll")]
    [InlineData("nested/arm64/native.dll")]
    public async Task AssertShippingPayload_RejectsForbiddenNamesAndPathSegments(string relativePath)
    {
        var root = NewTempDirectory();
        try
        {
            WriteFile(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            var result = await RunPayloadGuardAsync(root);

            result.ExitCode.Should().Be(1);
            result.Error.Should().Contain("Shipping payload contains test or validation tool artifacts:");
            result.Error.Should().Contain(Path.GetFileName(relativePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PruneShippingFootprint_WindowsX64_RemovesPdbsNonTargetNativesAndUnsupportedCultures()
    {
        var root = NewTempDirectory();
        try
        {
            WriteFile(root, "x86", "native.dll");
            WriteFile(root, "arm64", "native.dll");
            WriteFile(root, "en", "UniversalDeviceToolkit.Lib.resources.dll");
            WriteFile(root, "fr", "UniversalDeviceToolkit.Lib.resources.dll");
            WriteFile(root, "de", "UniversalDeviceToolkit.Lib.resources.dll");
            WriteFile(root, "UniversalDeviceToolkit.Host.pdb");
            WriteFile(root, "nested", "debug.pdb");

            RunPrune(root, "win-x64", "en;fr");

            Directory.Exists(Path.Combine(root, "x86")).Should().BeFalse();
            Directory.Exists(Path.Combine(root, "arm64")).Should().BeFalse();
            Directory.Exists(Path.Combine(root, "en")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "fr")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "de")).Should().BeFalse();
            Directory.EnumerateFiles(root, "*.pdb", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PruneShippingFootprint_RemovesDebuggerDumpHelpersAndDocumentationXml()
    {
        var root = NewTempDirectory();
        try
        {
            WriteFile(root, "createdump.exe");
            WriteFile(root, "mscordaccore.dll");
            WriteFile(root, "mscordaccore_amd64_amd64_10.0.0.0.dll");
            WriteFile(root, "libmscordaccore.so");
            WriteFile(root, "libmscordaccore.dylib");
            WriteFile(root, "mscordbi.dll");
            WriteFile(root, "dbgshim.dll");
            WriteFile(root, "Microsoft.DiaSymReader.Native.amd64.dll");
            WriteFile(root, "libMonoPosixHelper.dll");
            WriteFile(root, "Mono.Posix.NETStandard.dll");
            WriteFile(root, "UniversalDeviceToolkit.Lib.dll");
            WriteFile(root, "UniversalDeviceToolkit.Lib.xml");
            WriteFile(root, "keep-config.xml");

            RunPrune(root, "win-x64", "en");

            File.Exists(Path.Combine(root, "createdump.exe")).Should().BeFalse();
            File.Exists(Path.Combine(root, "mscordaccore.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "mscordaccore_amd64_amd64_10.0.0.0.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "libmscordaccore.so")).Should().BeFalse();
            File.Exists(Path.Combine(root, "libmscordaccore.dylib")).Should().BeFalse();
            File.Exists(Path.Combine(root, "mscordbi.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "dbgshim.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "Microsoft.DiaSymReader.Native.amd64.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "libMonoPosixHelper.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "Mono.Posix.NETStandard.dll")).Should().BeFalse();
            File.Exists(Path.Combine(root, "UniversalDeviceToolkit.Lib.xml")).Should().BeFalse();
            File.Exists(Path.Combine(root, "UniversalDeviceToolkit.Lib.dll")).Should().BeTrue();
            File.Exists(Path.Combine(root, "keep-config.xml")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PruneShippingFootprint_NonWindowsRid_PreservesNativeDirectoriesAndFiltersCultures()
    {
        var root = NewTempDirectory();
        try
        {
            WriteFile(root, "x86", "native.dll");
            WriteFile(root, "arm64", "native.dll");
            WriteFile(root, "en", "UniversalDeviceToolkit.Lib.resources.dll");
            WriteFile(root, "fr", "UniversalDeviceToolkit.Lib.resources.dll");
            WriteFile(root, "UniversalDeviceToolkit.Host.pdb");

            RunPrune(root, "linux-x64", "fr");

            Directory.Exists(Path.Combine(root, "x86")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "arm64")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "en")).Should().BeFalse();
            Directory.Exists(Path.Combine(root, "fr")).Should().BeTrue();
            Directory.EnumerateFiles(root, "*.pdb", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Encoding GetMarkerEncoding(string encodingName) => encodingName switch
    {
        "utf8" => Encoding.UTF8,
        "utf16le" => Encoding.Unicode,
        "utf16be" => Encoding.BigEndianUnicode,
        _ => throw new ArgumentOutOfRangeException(nameof(encodingName), encodingName, "Unknown marker encoding."),
    };

    private static async Task<(int ExitCode, string Output, string Error)> RunPayloadGuardAsync(string payloadPath)
    {
        var scriptPath = Path.Combine(RepositoryPaths.FindRoot(), "Scripts", "Assert-ShippingPayload.ps1");
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath,
                     "-PayloadPath", payloadPath })
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the shipping payload guard.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("The shipping payload guard did not complete within 30 seconds.");
        }

        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static void RunPrune(string payloadPath, string runtimeIdentifier, string allowedCultures)
    {
        var scriptPath = Path.Combine(RepositoryPaths.FindRoot(), "Scripts", "Prune-ShippingFootprint.ps1");
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-PayloadPath");
        startInfo.ArgumentList.Add(payloadPath);
        startInfo.ArgumentList.Add("-RuntimeIdentifier");
        startInfo.ArgumentList.Add(runtimeIdentifier);
        startInfo.ArgumentList.Add("-AllowedCultures");
        startInfo.ArgumentList.Add(allowedCultures);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the prune script.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000).Should().BeTrue("the prune script must complete promptly");
        process.ExitCode.Should().Be(0, "PowerShell output was:{0}{1}{2}", Environment.NewLine, output, error);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"UDT-footprint-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string root, params string[] pathParts)
    {
        var path = Path.Combine([root, .. pathParts]);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Test file has no parent directory: {path}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "fixture");
    }
}
