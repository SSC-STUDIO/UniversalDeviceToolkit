using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class StringExtensionsTests
{
    #region GetUntilOrEmpty Tests

    [Fact]
    public void GetUntilOrEmpty_WhenStringHasStopCharacter_ShouldReturnSubstringBeforeStop()
    {
        // Arrange
        var text = "hello@world.com";
        var stopAt = "@";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public void GetUntilOrEmpty_WhenStringDoesNotHaveStopCharacter_ShouldReturnEmpty()
    {
        // Arrange
        var text = "helloworld.com";
        var stopAt = "@";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUntilOrEmpty_WhenStopCharacterIsAtStart_ShouldReturnEmpty()
    {
        // Arrange
        var text = "@test";
        var stopAt = "@";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUntilOrEmpty_WhenTextIsNull_ShouldReturnEmpty()
    {
        // Arrange
        string? text = null;
        var stopAt = "@";

        // Act
        var result = text!.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUntilOrEmpty_WhenTextIsEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var text = "";
        var stopAt = "@";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUntilOrEmpty_WhenTextIsWhitespace_ShouldReturnEmpty()
    {
        // Arrange
        var text = "   ";
        var stopAt = "@";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUntilOrEmpty_WithMultipleStopCharacters_ShouldReturnFirstSegment()
    {
        // Arrange
        var text = "path/to/file.txt";
        var stopAt = "/";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().Be("path");
    }

    [Fact]
    public void GetUntilOrEmpty_WithEmptyStopAt_ShouldReturnEmpty()
    {
        // Arrange
        var text = "hello world";
        var stopAt = "";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUntilOrEmpty_WithLongStopString_ShouldReturnSubstring()
    {
        // Arrange
        var text = "helloSTOPworld";
        var stopAt = "STOP";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public void GetUntilOrEmpty_WithSpecialCharacters_ShouldWork()
    {
        // Arrange
        var text = "key=value";
        var stopAt = "=";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().Be("key");
    }

    [Fact]
    public void GetUntilOrEmpty_WithUnicodeCharacters_ShouldWork()
    {
        // Arrange
        var text = "你好世界@test";
        var stopAt = "@";

        // Act
        var result = text.GetUntilOrEmpty(stopAt);

        // Assert
        result.Should().Be("你好世界");
    }

    #endregion
}
