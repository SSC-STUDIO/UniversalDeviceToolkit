using UniversalDeviceToolkit.Tests;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

[Collection(TestCollections.ProcessState)]
public sealed class HostDataDirectoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("C:\\Existing user data")]
    public async Task HostStartup_UsesShellDataDirectoryInsteadOfAmbientProfile(string? ambientDirectory)
    {
        using var scope = new EnvironmentVariableScope("UDT_APPDATA_OVERRIDE", ambientDirectory);
        var diagnosticData = Path.Combine(Path.GetTempPath(), "UDT isolated diagnostic");
        await using var host = new HostConnection("C:\\UDT\\Host.exe", ["--no-hardware", "--safe-start"], diagnosticData, _ => { }, diagnostic: true);

        var start = host.CreateStartInfo();

        Assert.Equal(diagnosticData, start.Environment["UDT_APPDATA_OVERRIDE"]);
        Assert.Equal("1", start.Environment["UDT_DIAGNOSTIC_MODE"]);
        Assert.Equal(new[] { "--no-hardware", "--safe-start" }, start.ArgumentList);
        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardInput);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData(null, false)]
    [InlineData("0", false)]
    public void DiagnosticMarker_DisablesHardwareAndRequestsSafeStartOnlyWhenExplicitlyEnabled(string? marker, bool expected)
    {
        using var scope = new EnvironmentVariableScope("UDT_DIAGNOSTIC_MODE", marker);
        var flags = HostFlags.Parse([]);
        Assert.Equal(expected, flags.Diagnostic);
        Assert.Equal(expected, flags.NoHardware);
        Assert.Equal(expected, flags.SafeStart);
    }
}
