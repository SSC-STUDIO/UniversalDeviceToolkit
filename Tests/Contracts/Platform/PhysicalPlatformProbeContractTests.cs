using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Platform;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Platform;

[Trait("Category", TestCategories.Guard)]
public sealed class PhysicalPlatformProbeContractTests
{
    [Fact]
    public void Probe_WithNullOrEmptyPaths_DoesNotThrow()
    {
        var probe = new PhysicalPlatformProbe();

        probe.FileExists(null!).Should().BeFalse();
        probe.FileExists("").Should().BeFalse();
        probe.DirectoryExists(null!).Should().BeFalse();
        probe.EnumerateFiles(null!, "*.dll").Should().BeEmpty();
        probe.EnumerateFiles(Path.GetTempPath(), null!).Should().BeEmpty();
        probe.EnumerateDirectories(null!).Should().BeEmpty();
    }

    [Fact]
    public void CommandRunner_WithMissingFileName_ReturnsErrorResult()
    {
        var runner = new ProcessPlatformCommandRunner();
        var result = runner.Run("   ");
        result.Succeeded.Should().BeFalse();
        result.StandardError.Should().Be("File name is required.");
    }
}
