using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Security)]
public sealed class FanCurveManagerSecurityGuardTests
{
    [Fact]
    public void LegacyFanCurveLoader_ShouldVerifyAuthenticodeBeforeAssemblyLoad()
    {
        var source = RepositoryPaths.ReadFile("Libraries/Device", "Cooling", "FanCurveManager.cs");
        var verifyIndex = source.IndexOf("FanCurveAssemblySignatureVerifier.TryVerifyFile", StringComparison.Ordinal);
        var loadIndex = source.IndexOf("Assembly.LoadFrom", StringComparison.Ordinal);

        verifyIndex.Should().BeGreaterThanOrEqualTo(0);
        loadIndex.Should().BeGreaterThanOrEqualTo(0);
        verifyIndex.Should().BeLessThan(loadIndex);
        source.Should().Contain("SearchOption.TopDirectoryOnly");
        source.Should().Contain("Refusing to load unsigned or invalid fan-curve extension");
    }

    [Fact]
    public void FanCurveSignatureVerifier_ShouldUseWinVerifyTrust()
    {
        var source = RepositoryPaths.ReadFile(
            "Libraries/Device", "Cooling", "FanCurveAssemblySignatureVerifier.cs");

        source.Should().Contain("WinVerifyTrust");
        source.Should().Contain("WtdStateActionClose");
        source.Should().Contain("WtdDisableMd2Md4");
    }
}
