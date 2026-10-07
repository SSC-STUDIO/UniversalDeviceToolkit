using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class DiagnosticHostBuildTests
{
    [Theory]
    [InlineData("6.1.4+9efce49b3", "6.1.4+8f3f4fcb8")]
    [InlineData("6.1.4+9efce49b3", "6.1.3+9efce49b3")]
    [InlineData("6.1.4+unknown", "6.1.4+unknown")]
    [InlineData("6.1.4", "6.1.4")]
    [InlineData(null, null)]
    [InlineData("6.1.4+9efce49b3", null)]
    public void DiagnosticStartup_RejectsStaleOrUnverifiableHost(string? shellVersion, string? hostVersion)
    {
        var error = Assert.Throws<InvalidOperationException>(() => HostConnection.ValidateDiagnosticBuild(shellVersion, hostVersion));
        Assert.Contains("Rebuild both from the same commit", error.Message);
    }

    [Theory]
    [InlineData("6.1.4+9efce49b3")]
    [InlineData("6.1.4+9efce49b3f25f8d32b6d3af10f4efd4f9724afa4")]
    public void DiagnosticStartup_AcceptsMatchingVerifiedBuilds(string version)
    {
        HostConnection.ValidateDiagnosticBuild(version, version);
    }
}
