using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.GameDetection;

/// <summary>
/// Unit tests for ProcessInfo struct.
/// Note: GameConfigStoreDetector and EffectiveGameModeDetector are internal classes
/// and cannot be tested from the external test project.
/// </summary>
public class GameDetectorTests
{
    #region ProcessInfo.FromPath Tests

    [Fact]
    public void FromPath_WithValidPath_ExtractsNameWithoutExtension()
    {
        // Arrange
        var path = @"C:\Games\MyGame.exe";

        // Act
        var result = ProcessInfo.FromPath(path);

        // Assert
        result.Name.Should().Be("MyGame");
        result.ExecutablePath.Should().Be(path);
    }

    [Fact]
    public void FromPath_WithDllExtension_ExtractsNameWithoutExtension()
    {
        // Arrange
        var path = @"C:\Libraries\engine.dll";

        // Act
        var result = ProcessInfo.FromPath(path);

        // Assert
        result.Name.Should().Be("engine");
        result.ExecutablePath.Should().Be(path);
    }

    [Fact]
    public void FromPath_WithNoExtension_ReturnsFullName()
    {
        // Arrange
        var path = @"C:\Bin\program";

        // Act
        var result = ProcessInfo.FromPath(path);

        // Assert
        result.Name.Should().Be("program");
        result.ExecutablePath.Should().Be(path);
    }

    [Fact]
    public void FromPath_WithNestedDirectories_ExtractsFileName()
    {
        // Arrange
        var path = @"C:\Program Files\Game Studio\Game\bin\game.exe";

        // Act
        var result = ProcessInfo.FromPath(path);

        // Assert
        result.Name.Should().Be("game");
        result.ExecutablePath.Should().Be(path);
    }

    #endregion

    #region ProcessInfo Equality Tests

    [Fact]
    public void Equals_WithSameNameAndPath_ReturnsTrue()
    {
        // Arrange
        var info1 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");
        var info2 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");

        // Act & Assert
        info1.Should().Be(info2);
        (info1 == info2).Should().BeTrue();
        (info1 != info2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentName_ReturnsFalse()
    {
        // Arrange
        var info1 = new ProcessInfo("GameA", @"C:\Games\Game.exe");
        var info2 = new ProcessInfo("GameB", @"C:\Games\Game.exe");

        // Act & Assert
        info1.Should().NotBe(info2);
        (info1 == info2).Should().BeFalse();
        (info1 != info2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentPath_ReturnsFalse()
    {
        // Arrange
        var info1 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");
        var info2 = new ProcessInfo("MyGame", @"D:\Games\MyGame.exe");

        // Act & Assert
        info1.Should().NotBe(info2);
    }

    [Fact]
    public void GetHashCode_WithEqualObjects_ReturnsSameHash()
    {
        // Arrange
        var info1 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");
        var info2 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");

        // Act & Assert
        info1.GetHashCode().Should().Be(info2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentObjects_LikelyDifferentHash()
    {
        // Arrange
        var info1 = new ProcessInfo("GameA", @"C:\Games\A.exe");
        var info2 = new ProcessInfo("GameB", @"C:\Games\B.exe");

        // Act & Assert
        // Not guaranteed but overwhelmingly likely
        info1.GetHashCode().Should().NotBe(info2.GetHashCode());
    }

    #endregion

    #region ProcessInfo Comparison Tests

    [Fact]
    public void CompareTo_WithSameValues_ReturnsZero()
    {
        // Arrange
        var info1 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");
        var info2 = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");

        // Act
        var result = info1.CompareTo(info2);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CompareTo_WithNameAlphabeticallyLessThanOther_ReturnsNegative()
    {
        // Arrange
        var info1 = new ProcessInfo("Alpha", @"C:\A.exe");
        var info2 = new ProcessInfo("Beta", @"C:\B.exe");

        // Act
        var result = info1.CompareTo(info2);

        // Assert
        result.Should().BeNegative();
    }

    [Fact]
    public void CompareTo_WithNameAlphabeticallyGreaterThanOther_ReturnsPositive()
    {
        // Arrange
        var info1 = new ProcessInfo("Zeta", @"C:\Z.exe");
        var info2 = new ProcessInfo("Alpha", @"C:\A.exe");

        // Act
        var result = info1.CompareTo(info2);

        // Assert
        result.Should().BePositive();
    }

    [Fact]
    public void OperatorLessThan_WithLessName_ReturnsTrue()
    {
        // Arrange
        var info1 = new ProcessInfo("Alpha", @"C:\A.exe");
        var info2 = new ProcessInfo("Beta", @"C:\B.exe");

        // Act & Assert
        (info1 < info2).Should().BeTrue();
        (info1 <= info2).Should().BeTrue();
    }

    [Fact]
    public void OperatorGreaterThan_WithGreaterName_ReturnsTrue()
    {
        // Arrange
        var info1 = new ProcessInfo("Zeta", @"C:\Z.exe");
        var info2 = new ProcessInfo("Alpha", @"C:\A.exe");

        // Act & Assert
        (info1 > info2).Should().BeTrue();
        (info1 >= info2).Should().BeTrue();
    }

    #endregion

    #region ProcessInfo ToString Tests

    [Fact]
    public void ToString_ContainsNameAndPath()
    {
        // Arrange
        var info = new ProcessInfo("MyGame", @"C:\Games\MyGame.exe");

        // Act
        var result = info.ToString();

        // Assert
        result.Should().Contain("MyGame");
        result.Should().Contain(@"C:\Games\MyGame.exe");
    }

    #endregion

    #region ProcessInfo HashSet Tests

    [Fact]
    public void HashSet_WithDuplicateFromPath_AddsOnlyOne()
    {
        // Arrange
        var set = new HashSet<ProcessInfo>();

        // Act
        set.Add(ProcessInfo.FromPath(@"C:\Games\MyGame.exe"));
        set.Add(ProcessInfo.FromPath(@"C:\Games\MyGame.exe"));

        // Assert
        set.Count.Should().Be(1);
    }

    [Fact]
    public void HashSet_WithDifferentPaths_AddsBoth()
    {
        // Arrange
        var set = new HashSet<ProcessInfo>();

        // Act
        set.Add(ProcessInfo.FromPath(@"C:\Games\GameA.exe"));
        set.Add(ProcessInfo.FromPath(@"C:\Games\GameB.exe"));

        // Assert
        set.Count.Should().Be(2);
    }

    [Fact]
    public void HashSet_WithSameNameDifferentPath_AddsBoth()
    {
        // Arrange - Two different executables that happen to share a name (different paths)
        var set = new HashSet<ProcessInfo>();

        // Act
        set.Add(ProcessInfo.FromPath(@"C:\Steam\game.exe"));
        set.Add(ProcessInfo.FromPath(@"D:\Epic\game.exe"));

        // Assert - different paths means different ProcessInfo
        set.Count.Should().Be(2);
    }

    #endregion
}
