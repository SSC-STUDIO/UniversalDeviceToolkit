using FluentAssertions;
using UniversalDeviceToolkit.Shared.Utils;
using UniversalDeviceToolkit.Tests;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class PathSecurityLinkTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsPathWithinAllowedDirectory_WhenAncestorEscapesRoot_ShouldReject(bool leafExists)
    {
        using var sandbox = new LinkedSandbox();
        if (leafExists)
            File.WriteAllText(Path.Combine(sandbox.Outside, "settings.json"), "{}");

        var path = Path.Combine(sandbox.EscapeLink, "settings.json");

        PathSecurity.IsPathWithinAllowedDirectory(path, sandbox.Allowed).Should().BeFalse();
        PathSecurity.CreateSafeFilePath(sandbox.EscapeLink, "settings.json").Should().NotBeNull(
            "a linked directory explicitly selected as the allowed root is valid");
    }

    [Fact]
    public void IsPathWithinAllowedDirectory_WhenAllowedRootIsLinked_ShouldAcceptItsChildren()
    {
        using var sandbox = new LinkedSandbox();
        var rootLink = Path.Combine(sandbox.Root, "root-link");
        sandbox.CreateLink(rootLink, sandbox.Allowed);

        PathSecurity.IsPathWithinAllowedDirectory(Path.Combine(rootLink, "new", "settings.json"), rootLink)
            .Should().BeTrue();
        PathSecurity.IsPathWithinAllowedDirectory(Path.Combine(rootLink, "escape", "settings.json"), rootLink)
            .Should().BeFalse();
    }

    [Fact]
    public void IsPathWithinAllowedDirectory_WhenAncestorLinksInsideRoot_ShouldAccept()
    {
        using var sandbox = new LinkedSandbox();
        var realDirectory = Path.Combine(sandbox.Allowed, "real");
        Directory.CreateDirectory(realDirectory);
        var link = Path.Combine(sandbox.Allowed, "internal");
        sandbox.CreateLink(link, realDirectory);

        PathSecurity.IsPathWithinAllowedDirectory(Path.Combine(link, "new", "settings.json"), sandbox.Allowed)
            .Should().BeTrue();
    }

    private sealed class LinkedSandbox : IDisposable
    {
        private readonly List<string> _links = new();
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "udt-path-security-" + Guid.NewGuid().ToString("N"));
        public string Allowed => Path.Combine(Root, "allowed");
        public string Outside => Path.Combine(Root, "outside");
        public string EscapeLink => Path.Combine(Allowed, "escape");

        public LinkedSandbox()
        {
            Directory.CreateDirectory(Allowed);
            Directory.CreateDirectory(Outside);
            CreateLink(EscapeLink, Outside);
        }

        public void CreateLink(string link, string target)
        {
            DirectoryLinkTestHelper.Create(link, target);
            _links.Add(link);
        }

        public void Dispose()
        {
            foreach (var link in _links)
                Directory.Delete(link);
            Directory.Delete(Root, recursive: true);
        }
    }
}
