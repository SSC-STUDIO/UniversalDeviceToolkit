using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.GameDetection;

/// <summary>
/// Unit tests for the GameDetection module via reflection (classes are internal).
/// Tests cover GameConfigStoreDetector lifecycle, game detection logic, and ProcessInfo equality.
/// </summary>
public class GameDetectionTests
{
    private static readonly Assembly LibAssembly = typeof(ProcessInfo).Assembly;

    private static Type GetGameConfigStoreDetectorType()
    {
        return LibAssembly.GetType("UniversalDeviceToolkit.Lib.GameDetection.GameConfigStoreDetector")
            ?? throw new InvalidOperationException("GameConfigStoreDetector type not found");
    }

    private static Type GetEffectiveGameModeDetectorType()
    {
        return LibAssembly.GetType("UniversalDeviceToolkit.Lib.GameDetection.EffectiveGameModeDetector")
            ?? throw new InvalidOperationException("EffectiveGameModeDetector type not found");
    }

    private static Type GetGameDetectedEventArgsType()
    {
        return LibAssembly.GetType("UniversalDeviceToolkit.Lib.GameDetection.GameConfigStoreDetector+GameDetectedEventArgs")
            ?? throw new InvalidOperationException("GameDetectedEventArgs type not found");
    }

    #region GameConfigStoreDetector.GetDetectedGamePaths Tests

    [Fact]
    public void GetDetectedGamePaths_WhenCalled_ReturnsHashSetOfProcessInfo()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var method = type.GetMethod("GetDetectedGamePaths", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetDetectedGamePaths method not found");

        // Act
        var result = method.Invoke(null, null);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<HashSet<ProcessInfo>>();
    }

    [Fact]
    public void GetDetectedGamePaths_WhenCalledMultipleTimes_ReturnsConsistentType()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var method = type.GetMethod("GetDetectedGamePaths", BindingFlags.Public | BindingFlags.Static)!;

        // Act
        var result1 = (HashSet<ProcessInfo>)method.Invoke(null, null)!;
        var result2 = (HashSet<ProcessInfo>)method.Invoke(null, null)!;

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.GetType().Should().Be(result2.GetType());
    }

    [Fact]
    public void GetDetectedGamePaths_ReturnsUniqueEntries()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var method = type.GetMethod("GetDetectedGamePaths", BindingFlags.Public | BindingFlags.Static)!;

        // Act
        var result = (HashSet<ProcessInfo>)method.Invoke(null, null)!;

        // Assert - HashSet guarantees uniqueness
        result.Count.Should().Be(result.Distinct().Count());
    }

    #endregion

    #region GameConfigStoreDetector Start/Stop Lifecycle

    [Fact]
    public async Task GameConfigStoreDetector_StartAsync_WhenCalledTwice_DoesNotThrow()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var instance = Activator.CreateInstance(type, nonPublic: true)
            ?? throw new InvalidOperationException("Failed to create GameConfigStoreDetector");
        var startMethod = type.GetMethod("StartAsync")!;
        var stopMethod = type.GetMethod("StopAsync")!;

        // Act
        await (Task)startMethod.Invoke(instance, null)!;
        await (Task)startMethod.Invoke(instance, null)!;

        // Cleanup
        await (Task)stopMethod.Invoke(instance, null)!;
    }

    [Fact]
    public async Task GameConfigStoreDetector_StopAsync_WhenNotStarted_DoesNotThrow()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var stopMethod = type.GetMethod("StopAsync")!;

        // Act - should be safe to stop without starting
        var act = async () => await (Task)stopMethod.Invoke(instance, null)!;

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GameConfigStoreDetector_StartThenStop_DoesNotThrow()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var startMethod = type.GetMethod("StartAsync")!;
        var stopMethod = type.GetMethod("StopAsync")!;

        // Act
        await (Task)startMethod.Invoke(instance, null)!;
        await (Task)stopMethod.Invoke(instance, null)!;

        // Second stop should also be safe
        await (Task)stopMethod.Invoke(instance, null)!;
    }

    #endregion

    #region GameDetectedEventArgs Tests

    [Fact]
    public void GameDetectedEventArgs_WithGames_SetsGamesProperty()
    {
        // Arrange
        var argsType = GetGameDetectedEventArgsType();
        var games = new HashSet<ProcessInfo>();
        var constructor = argsType.GetConstructors().First();

        // Act
        var args = constructor.Invoke([games]);

        // Assert
        var gamesProperty = argsType.GetProperty("Games")!;
        var actualGames = gamesProperty.GetValue(args);
        actualGames.Should().BeSameAs(games);
    }

    [Fact]
    public void GamesDetected_Event_CanBeSubscribedAndUnsubscribed()
    {
        // Arrange
        var type = GetGameConfigStoreDetectorType();
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var eventInfo = type.GetEvent("GamesDetected")!;
        var delegateType = eventInfo.EventHandlerType!;
        var handler = Delegate.CreateDelegate(delegateType, typeof(GameDetectionTests).GetMethod(nameof(NoOpEventHandler), BindingFlags.NonPublic | BindingFlags.Static)!);

        // Act
        eventInfo.AddEventHandler(instance, handler);
        eventInfo.RemoveEventHandler(instance, handler);

        // Assert - no exception thrown
    }

    private static void NoOpEventHandler(object sender, EventArgs args) { }

    #endregion

    #region EffectiveGameModeDetector Tests

    [Fact]
    public void EffectiveGameModeDetector_Constructor_DoesNotThrow()
    {
        // Arrange
        var type = GetEffectiveGameModeDetectorType();

        // Act
        var act = () => Activator.CreateInstance(type, nonPublic: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EffectiveGameModeDetector_HasChangedEvent()
    {
        // Arrange
        var type = GetEffectiveGameModeDetectorType();

        // Act & Assert
        var changedEvent = type.GetEvent("Changed");
        changedEvent.Should().NotBeNull();
        changedEvent!.EventHandlerType.Should().NotBeNull();
    }

    [Fact]
    public async Task EffectiveGameModeDetector_StartAsync_DoesNotThrow()
    {
        // Arrange
        var type = GetEffectiveGameModeDetectorType();
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var startMethod = type.GetMethod("StartAsync")!;
        var stopMethod = type.GetMethod("StopAsync")!;

        try
        {
            await (Task)startMethod.Invoke(instance, null)!;
        }
        finally
        {
            await (Task)stopMethod.Invoke(instance, null)!;
        }
    }

    #endregion

    #region ProcessInfo Tests (public struct used by GameDetection)

    [Fact]
    public void ProcessInfo_FromPath_ExtractsNameFromPath()
    {
        // Act
        var info = ProcessInfo.FromPath(@"C:\Games\MyGame.exe");

        // Assert
        info.Name.Should().Be("MyGame");
        info.ExecutablePath.Should().Be(@"C:\Games\MyGame.exe");
    }

    [Fact]
    public void ProcessInfo_Equality_SameValues_AreEqual()
    {
        // Arrange
        var info1 = new ProcessInfo("game", @"C:\game.exe");
        var info2 = new ProcessInfo("game", @"C:\game.exe");

        // Act & Assert
        info1.Should().Be(info2);
        (info1 == info2).Should().BeTrue();
    }

    [Fact]
    public void ProcessInfo_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var info1 = new ProcessInfo("game1", @"C:\game1.exe");
        var info2 = new ProcessInfo("game2", @"C:\game2.exe");

        // Act & Assert
        info1.Should().NotBe(info2);
        (info1 != info2).Should().BeTrue();
    }

    [Fact]
    public void ProcessInfo_GetHashCode_SameForEqualInstances()
    {
        // Arrange
        var info1 = new ProcessInfo("game", @"C:\game.exe");
        var info2 = new ProcessInfo("game", @"C:\game.exe");

        // Act & Assert
        info1.GetHashCode().Should().Be(info2.GetHashCode());
    }

    [Fact]
    public void ProcessInfo_ToString_ContainsNameAndPath()
    {
        // Arrange
        var info = new ProcessInfo("test", @"C:\test.exe");

        // Act
        var result = info.ToString();

        // Assert
        result.Should().Contain("test");
        result.Should().Contain(@"C:\test.exe");
    }

    #endregion
}
