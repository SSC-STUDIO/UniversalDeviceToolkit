using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class NetworkProxyWorkerLauncherTests
{
    [Fact]
    public void TryMapHostDirectoryToNetworkProxyDirectory_RewritesHostProjectFolder()
    {
        var hostOut = @"C:\src\UniversalDeviceToolkit\Apps\Host\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64";
        var mapped = NetworkProxyWorkerLauncher.TryMapHostDirectoryToNetworkProxyDirectory(hostOut);

        mapped.Should().Be(
            @"C:\src\UniversalDeviceToolkit\Apps\NetworkProxy\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64");
    }

    [Fact]
    public void TryMapHostDirectoryToNetworkProxyDirectory_AcceptsTrailingSeparator()
    {
        var hostOut = @"C:\src\UniversalDeviceToolkit\Apps\Host\bin\x64\Debug\";
        var mapped = NetworkProxyWorkerLauncher.TryMapHostDirectoryToNetworkProxyDirectory(hostOut);

        mapped.Should().Be(@"C:\src\UniversalDeviceToolkit\Apps\NetworkProxy\bin\x64\Debug");
    }

    [Fact]
    public void TryMapHostDirectoryToNetworkProxyDirectory_WhenUnrelated_ReturnsNull()
    {
        NetworkProxyWorkerLauncher.TryMapHostDirectoryToNetworkProxyDirectory(@"C:\Windows\System32")
            .Should().BeNull();
        NetworkProxyWorkerLauncher.TryMapHostDirectoryToNetworkProxyDirectory(null)
            .Should().BeNull();
        NetworkProxyWorkerLauncher.TryMapHostDirectoryToNetworkProxyDirectory("   ")
            .Should().BeNull();
    }

    [Fact]
    public void EnumerateWorkerCandidates_IncludesSiblingNetworkProxyOutput()
    {
        var hostOut = @"C:\src\UniversalDeviceToolkit\Apps\Host\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64";
        var candidates = NetworkProxyWorkerLauncher.EnumerateWorkerCandidates(
                programDirectory: hostOut,
                baseDirectory: hostOut,
                currentDirectory: hostOut)
            .ToArray();

        candidates.Should().Contain(Path.Combine(hostOut, NetworkProxyWorkerLauncher.WorkerFileName));
        candidates.Should().Contain(
            @"C:\src\UniversalDeviceToolkit\Apps\NetworkProxy\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\UniversalDeviceToolkit.NetworkProxy.exe");
    }

    [Fact]
    public void IsRunnableWorker_RequiresExeAndSidecars()
    {
        var directory = Path.Combine(Path.GetTempPath(), "udt-network-proxy-launcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var exe = Path.Combine(directory, NetworkProxyWorkerLauncher.WorkerFileName);
            File.WriteAllBytes(exe, [0]);
            NetworkProxyWorkerLauncher.IsRunnableWorker(exe).Should().BeFalse();

            File.WriteAllText(Path.ChangeExtension(exe, ".runtimeconfig.json"), "{}");
            NetworkProxyWorkerLauncher.IsRunnableWorker(exe).Should().BeFalse();

            File.WriteAllText(Path.ChangeExtension(exe, ".deps.json"), "{}");
            NetworkProxyWorkerLauncher.IsRunnableWorker(exe).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
