using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.AutoListeners;

[Trait("Category", TestCategories.Unit)]
public class ProcessAutoListenerTests
{
    [Fact]
    public void CleanUpCacheIfNecessary_WhenCacheContainsDeadProcesses_ShouldNotModifyDuringEnumeration()
    {
        // Arrange
        var listener = new ProcessAutoListener(
            new InstanceStartedEventAutoAutoListener(),
            new InstanceStoppedEventAutoAutoListener());

        var cache = GetCache(listener);
        for (var i = 0; i < 250; i++)
            cache[10_000_000 + i] = new ProcessInfo($"dead-{i}", null);

        var method = typeof(ProcessAutoListener)
            .GetMethod("CleanUpCacheIfNecessary", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Act
        Action act = () => method.Invoke(listener, null);

        // Assert
        act.Should().NotThrow();
        cache.Should().BeEmpty();
    }

    private static Dictionary<int, ProcessInfo> GetCache(ProcessAutoListener listener) =>
        (Dictionary<int, ProcessInfo>)typeof(ProcessAutoListener)
            .GetField("_processCache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(listener)!;
}
