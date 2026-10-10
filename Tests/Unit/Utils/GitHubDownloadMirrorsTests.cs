using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class GitHubDownloadMirrorsTests
{
    [Fact]
    public void WithMirrorFallbacks_DirectUrlAlwaysFirst()
    {
        var candidates = GitHubDownloadMirrors.WithMirrorFallbacks(
            "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/download/plugin-catalog/custom-mouse-v1.0.17.zip").ToArray();

        candidates[0].Should().StartWith("https://github.com/");
        candidates.Length.Should().BeGreaterThan(1);
    }

    [Fact]
    public void WithMirrorFallbacks_GitHubReleaseUrl_HasMirrorVariants()
    {
        const string url = "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/download/plugin-catalog/x.zip";

        var candidates = GitHubDownloadMirrors.WithMirrorFallbacks(url).ToArray();

        candidates.Should().Contain(c => c.StartsWith("https://gh-proxy.com/") && c.EndsWith(url));
        candidates.Should().Contain(c => c.StartsWith("https://ghfast.top/") && c.EndsWith(url));
    }

    [Fact]
    public void WithMirrorFallbacks_RawGitHubUserContent_HasMirrorVariants()
    {
        const string url = "https://raw.githubusercontent.com/SSC-STUDIO/UniversalDeviceToolkit/master/resources/stable/catalog.json";

        var candidates = GitHubDownloadMirrors.WithMirrorFallbacks(url).ToArray();

        candidates.Length.Should().Be(3);
        candidates.Skip(1).Should().OnlyContain(c => c.EndsWith(url));
    }

    [Fact]
    public void WithMirrorFallbacks_NonGitHubUrl_NoMirrors()
    {
        var candidates = GitHubDownloadMirrors.WithMirrorFallbacks("https://cdn.jsdelivr.net/gh/x/y.zip").ToArray();

        candidates.Should().Equal("https://cdn.jsdelivr.net/gh/x/y.zip");
    }

    [Fact]
    public void WithMirrorFallbacks_MirrorUrl_NotWrappedAgain()
    {
        const string mirrored = "https://gh-proxy.com/https://github.com/x/y.zip";

        var candidates = GitHubDownloadMirrors.WithMirrorFallbacks(mirrored).ToArray();

        candidates.Should().Equal(mirrored);
    }

    [Fact]
    public void WithMirrorFallbacks_HttpUrl_NoMirrors()
    {
        var candidates = GitHubDownloadMirrors.WithMirrorFallbacks("http://github.com/x/y.zip").ToArray();

        candidates.Should().Equal("http://github.com/x/y.zip");
    }

    [Theory]
    [InlineData("gh-proxy.com", true)]
    [InlineData("ghfast.top", true)]
    [InlineData("github.com", false)]
    [InlineData("", false)]
    public void IsMirrorHost_ClassifiesHosts(string host, bool expected)
    {
        GitHubDownloadMirrors.IsMirrorHost(host).Should().Be(expected);
    }
}
