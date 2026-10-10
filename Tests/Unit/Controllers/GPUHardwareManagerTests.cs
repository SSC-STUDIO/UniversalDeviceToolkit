using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Controllers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GPUHardwareManagerTests : UnitTestBase
{
    private GPUHardwareManager _manager = null!;

    protected override void Setup()
    {
        _manager = new GPUHardwareManager();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _manager.Should().NotBeNull();
    }

    [Fact]
    public void Class_ShouldImplement_IGPUHardwareManager()
    {
        _manager.Should().BeAssignableTo<IGPUHardwareManager>();
    }

    [Fact]
    public async Task RestartGPUAsync_WithNullInstanceId_ShouldNotThrow()
    {
        var act = async () => await _manager.RestartGPUAsync(null!);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RestartGPUAsync_WithEmptyInstanceId_ShouldNotThrow()
    {
        var act = async () => await _manager.RestartGPUAsync(string.Empty);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RestartGPUAsync_WithWhitespaceInstanceId_ShouldNotThrow()
    {
        var act = async () => await _manager.RestartGPUAsync("   ");

        await act.Should().NotThrowAsync();
    }
}
