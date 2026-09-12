using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Controllers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GPUProcessManagerTests : UnitTestBase
{
    private GPUProcessManager _manager = null!;

    protected override void Setup()
    {
        _manager = new GPUProcessManager();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _manager.Should().NotBeNull();
    }

    [Fact]
    public void Class_ShouldImplement_IGPUProcessManager()
    {
        _manager.Should().BeAssignableTo<IGPUProcessManager>();
    }

    [Fact]
    public async Task KillGPUProcessesAsync_WithEmptyList_ShouldNotThrow()
    {
        var act = async () => await _manager.KillGPUProcessesAsync(Enumerable.Empty<Process>());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task KillGPUProcessesAsync_WithNullEnumerable_ShouldNotThrow()
    {
        var act = async () => await _manager.KillGPUProcessesAsync(null!);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task KillGPUProcessesAsync_WithAlreadyExitedProcess_ShouldNotThrow()
    {
        using var process = new Process();
        var processField = typeof(Process).GetField("_haveProcessHandle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processField?.SetValue(process, false);

        var act = async () => await _manager.KillGPUProcessesAsync([process]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task KillGPUProcessesAsync_WithMultipleProcesses_ShouldNotThrow()
    {
        using var process1 = new Process();
        using var process2 = new Process();
        using var process3 = new Process();

        var act = async () => await _manager.KillGPUProcessesAsync([process1, process2, process3]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task KillGPUProcessesAsync_ShouldHandleDuplicateProcesses()
    {
        using var process = new Process();

        var act = async () => await _manager.KillGPUProcessesAsync([process, process]);

        await act.Should().NotThrowAsync();
    }
}
