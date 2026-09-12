using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.PackageDownloader;

[Trait("Category", TestCategories.Unit)]
public class PackageTests
{
    [Fact]
    public void Properties_ShouldRetainInitValues()
    {
        var pkg = new Package
        {
            Id = "PKG001",
            Title = "BIOS Update",
            Description = "Critical BIOS firmware update",
            Version = "1.2.3",
            Category = "BIOS",
            FileName = "bios_update.exe",
            FileSize = "15 MB",
            FileCrc = "abc123",
            ReleaseDate = new DateTime(2025, 6, 1),
            Readme = "Fixes thermal issues",
            FileLocation = "https://example.com/bios",
            IsUpdate = true,
            Reboot = RebootType.Forced
        };

        pkg.Id.Should().Be("PKG001");
        pkg.Title.Should().Be("BIOS Update");
        pkg.Description.Should().Be("Critical BIOS firmware update");
        pkg.Version.Should().Be("1.2.3");
        pkg.Category.Should().Be("BIOS");
        pkg.FileName.Should().Be("bios_update.exe");
        pkg.FileSize.Should().Be("15 MB");
        pkg.FileCrc.Should().Be("abc123");
        pkg.ReleaseDate.Should().Be(new DateTime(2025, 6, 1));
        pkg.Readme.Should().Be("Fixes thermal issues");
        pkg.FileLocation.Should().Be("https://example.com/bios");
        pkg.IsUpdate.Should().BeTrue();
        pkg.Reboot.Should().Be(RebootType.Forced);
    }

    [Fact]
    public void FileCrc_Nullable_ShouldAcceptNull()
    {
        var pkg = new Package { FileCrc = null };
        pkg.FileCrc.Should().BeNull();
    }

    [Fact]
    public void Readme_Nullable_ShouldAcceptNull()
    {
        var pkg = new Package { Readme = null };
        pkg.Readme.Should().BeNull();
    }

    [Fact]
    public void Index_ShouldContainTitleDescriptionVersionCategoryFileName()
    {
        var pkg = new Package
        {
            Title = "BIOS Update",
            Description = "Critical update",
            Version = "1.2.3",
            Category = "BIOS",
            FileName = "bios.exe"
        };

        var index = pkg.Index;
        index.Should().Contain("BIOS Update");
        index.Should().Contain("Critical update");
        index.Should().Contain("1.2.3");
        index.Should().Contain("BIOS");
        index.Should().Contain("bios.exe");
    }

    [Fact]
    public void Index_CalledTwice_ShouldReturnSameInstance()
    {
        var pkg = new Package
        {
            Title = "Test",
            Description = "Desc",
            Version = "1.0",
            Category = "Cat",
            FileName = "file.exe"
        };

        var first = pkg.Index;
        var second = pkg.Index;
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Reboot_AllEnumValues_ShouldBeSettable()
    {
        var notRequired = new Package { Reboot = RebootType.NotRequired };
        var forced = new Package { Reboot = RebootType.Forced };
        var requested = new Package { Reboot = RebootType.Requested };
        var forcedPowerOff = new Package { Reboot = RebootType.ForcedPowerOff };
        var delayed = new Package { Reboot = RebootType.Delayed };

        notRequired.Reboot.Should().Be(RebootType.NotRequired);
        forced.Reboot.Should().Be(RebootType.Forced);
        requested.Reboot.Should().Be(RebootType.Requested);
        forcedPowerOff.Reboot.Should().Be(RebootType.ForcedPowerOff);
        delayed.Reboot.Should().Be(RebootType.Delayed);
    }
}
