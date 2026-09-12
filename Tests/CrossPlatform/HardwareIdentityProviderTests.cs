using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class HardwareIdentityProviderTests
{
    [Fact]
    public void LinuxProvider_ShouldReadDmiIdentity()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/dmi/id/sys_vendor"] = "Framework Computer Inc.\n",
            ["/sys/class/dmi/id/product_name"] = "Framework Laptop 16\n",
            ["/sys/class/dmi/id/product_version"] = "A8\n",
            ["/sys/class/dmi/id/product_serial"] = "FRAMEWORK-SERIAL\n",
        });

        var identity = new LinuxHardwareIdentityProvider(fileSystem).Read();

        identity.Vendor.Should().Be("Framework Computer Inc.");
        identity.ProductName.Should().Be("Framework Laptop 16");
        identity.Model.Should().Be("Framework Laptop 16 A8");
        identity.SerialNumber.Should().Be("FRAMEWORK-SERIAL");
        identity.Source.Should().Be("linux-dmi");
    }

    [Fact]
    public void LinuxProvider_ShouldIgnorePlaceholderValuesAndFallbackToBoard()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/dmi/id/sys_vendor"] = "To Be Filled By O.E.M.",
            ["/sys/class/dmi/id/product_name"] = "System Product Name",
            ["/sys/class/dmi/id/board_vendor"] = "ASRock",
            ["/sys/class/dmi/id/board_name"] = "B650M Pro RS",
        });

        var identity = new LinuxHardwareIdentityProvider(fileSystem).Read();

        identity.Vendor.Should().Be("ASRock");
        identity.ProductName.Should().Be("B650M Pro RS");
        identity.Model.Should().Be("B650M Pro RS");
        identity.SerialNumber.Should().BeEmpty();
    }

    [Fact]
    public void MacProvider_ShouldReadSystemProfilerIdentity()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, string>
        {
            ["sysctl -n hw.model"] = "MacBookPro18,3\n",
            ["system_profiler SPHardwareDataType"] = """
                Hardware:

                    Hardware Overview:

                      Model Name: MacBook Pro
                      Model Identifier: MacBookPro18,3
                      Chip: Apple M1 Pro
                      Serial Number (system): MAC-SERIAL
                """
        });

        var identity = new MacHardwareIdentityProvider(runner).Read();

        identity.Vendor.Should().Be("Apple Inc.");
        identity.Model.Should().Be("MacBook Pro MacBookPro18,3");
        identity.ProductName.Should().Be("MacBook Pro");
        identity.SerialNumber.Should().Be("MAC-SERIAL");
        identity.Source.Should().Be("macos-system-profiler");
    }

    [Fact]
    public void MacProvider_WhenCommandsFail_ShouldReturnAppleUnknownIdentity()
    {
        var identity = new MacHardwareIdentityProvider(new FakeCommandRunner(new Dictionary<string, string>())).Read();

        identity.Vendor.Should().Be("Apple Inc.");
        identity.Model.Should().BeEmpty();
        identity.ProductName.Should().BeEmpty();
        identity.SerialNumber.Should().BeEmpty();
        identity.Source.Should().Be("macos-system-profiler");
    }

    private sealed class FakeFileSystem(IReadOnlyDictionary<string, string> files) : IFileSystem
    {
        public string ReadAllText(string path) => files.TryGetValue(path, out var value) ? value : string.Empty;

        public IEnumerable<string> EnumerateDirectories(string path) => [];

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => [];

        public bool DirectoryExists(string path) =>
            files.Keys.Any(file => file.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal));

        public string GetFileName(string path) => path.TrimEnd('/', '\\').Split('/', '\\').Last();
    }

    private sealed class FakeCommandRunner(IReadOnlyDictionary<string, string> outputs) : ICommandRunner
    {
        public string Run(string fileName, params string[] arguments)
        {
            var key = string.Join(' ', new[] { fileName }.Concat(arguments));
            return outputs.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }
}
