using FluentAssertions;
using UniversalDeviceToolkit.Lib.System;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ProcessSchedulingTests : UnitTestBase
{
    [Fact]
    public void TrySetBackgroundEfficiency_CurrentProcess_ShouldNotThrow()
    {
        var pid = Environment.ProcessId;

        var enabled = ProcessScheduling.TrySetBackgroundEfficiency(pid, background: true);
        var restored = ProcessScheduling.TrySetBackgroundEfficiency(pid, background: false);

        enabled.Should().BeTrue();
        restored.Should().BeTrue();
    }

    [Fact]
    public void TrySetBackgroundEfficiency_InvalidPid_ShouldReturnFalse()
    {
        ProcessScheduling.TrySetBackgroundEfficiency(0, background: true).Should().BeFalse();
        ProcessScheduling.TrySetBackgroundEfficiency(-1, background: true).Should().BeFalse();
    }
}
