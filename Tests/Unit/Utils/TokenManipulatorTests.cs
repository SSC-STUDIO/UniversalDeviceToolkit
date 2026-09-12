using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class TokenManipulatorTests
{
    #region Constants Tests

    [Fact]
    public void SE_BACKUP_PRIVILEGE_ShouldHaveCorrectValue()
    {
        // Arrange & Act & Assert
        TokenManipulator.SE_BACKUP_PRIVILEGE.Should().Be("SeBackupPrivilege");
    }

    [Fact]
    public void SE_RESTORE_PRIVILEGE_ShouldHaveCorrectValue()
    {
        // Arrange & Act & Assert
        TokenManipulator.SE_RESTORE_PRIVILEGE.Should().Be("SeRestorePrivilege");
    }

    [Fact]
    public void SE_TAKE_OWNERSHIP_PRIVILEGE_ShouldHaveCorrectValue()
    {
        // Arrange & Act & Assert
        TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE.Should().Be("SeTakeOwnershipPrivilege");
    }

    [Fact]
    public void SE_SYSTEM_ENVIRONMENT_PRIVILEGE_ShouldHaveCorrectValue()
    {
        // Arrange & Act & Assert
        TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE.Should().Be("SeSystemEnvironmentPrivilege");
    }

    #endregion

    #region AddPrivileges Tests

    [Fact(Skip = "Depends on the current process token rights and runner privileges")]
    public void AddPrivileges_WithSinglePrivilege_ShouldReturnBoolean()
    {
        // Arrange & Act
        // Note: This test will fail if not running with appropriate permissions
        // In unit tests, we typically don't have admin privileges
        var result = TokenManipulator.AddPrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE);

        // Assert - Should return false in unit test environment (no admin rights)
        result.Should().BeFalse();
    }

    [Fact(Skip = "Depends on the current process token rights and runner privileges")]
    public void AddPrivileges_WithMultiplePrivileges_ShouldReturnBoolean()
    {
        // Arrange & Act
        var result = TokenManipulator.AddPrivileges(
            TokenManipulator.SE_BACKUP_PRIVILEGE,
            TokenManipulator.SE_RESTORE_PRIVILEGE,
            TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE
        );

        // Assert - Should return false in unit test environment
        result.Should().BeFalse();
    }

    [Fact]
    public void AddPrivileges_WithEmptyArray_ShouldReturnTrue()
    {
        // Arrange & Act
        var result = TokenManipulator.AddPrivileges();

        // Assert - Empty array should succeed (no privileges to adjust)
        result.Should().BeTrue();
    }

    [Fact]
    public void AddPrivileges_WithInvalidPrivilege_ShouldReturnFalse()
    {
        // Arrange & Act
        var result = TokenManipulator.AddPrivileges("InvalidPrivilegeName");

        // Assert - Invalid privilege should fail
        result.Should().BeFalse();
    }

    #endregion

    #region RemovePrivileges Tests

    [Fact(Skip = "Depends on the current process token rights and runner privileges")]
    public void RemovePrivileges_WithSinglePrivilege_ShouldReturnBoolean()
    {
        // Arrange & Act
        var result = TokenManipulator.RemovePrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE);

        // Assert - Should return false in unit test environment
        result.Should().BeFalse();
    }

    [Fact(Skip = "Depends on the current process token rights and runner privileges")]
    public void RemovePrivileges_WithMultiplePrivileges_ShouldReturnBoolean()
    {
        // Arrange & Act
        var result = TokenManipulator.RemovePrivileges(
            TokenManipulator.SE_BACKUP_PRIVILEGE,
            TokenManipulator.SE_RESTORE_PRIVILEGE,
            TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE
        );

        // Assert - Should return false in unit test environment
        result.Should().BeFalse();
    }

    [Fact]
    public void RemovePrivileges_WithEmptyArray_ShouldReturnTrue()
    {
        // Arrange & Act
        var result = TokenManipulator.RemovePrivileges();

        // Assert - Empty array should succeed
        result.Should().BeTrue();
    }

    [Fact]
    public void RemovePrivileges_WithInvalidPrivilege_ShouldReturnFalse()
    {
        // Arrange & Act
        var result = TokenManipulator.RemovePrivileges("InvalidPrivilegeName");

        // Assert - Invalid privilege should fail
        result.Should().BeFalse();
    }

    #endregion

    #region Integration Tests (Require Admin Privileges)

    [Fact(Skip = "Requires administrator privileges to run")]
    public void AddPrivileges_WithAdminRights_ShouldSucceed()
    {
        // This test would succeed when running with proper admin privileges
        // Arrange & Act
        var result = TokenManipulator.AddPrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Skip = "Requires administrator privileges to run")]
    public void RemovePrivileges_WithAdminRights_ShouldSucceed()
    {
        // This test would succeed when running with proper admin privileges
        // Arrange - First add the privilege
        TokenManipulator.AddPrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE);

        // Act - Then remove it
        var result = TokenManipulator.RemovePrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Skip = "Requires administrator privileges to run")]
    public void AddAndRemovePrivileges_WithAdminRights_ShouldWorkCorrectly()
    {
        // Arrange
        var privileges = new[]
        {
            TokenManipulator.SE_BACKUP_PRIVILEGE,
            TokenManipulator.SE_RESTORE_PRIVILEGE
        };

        // Act - Add privileges
        var addResult = TokenManipulator.AddPrivileges(privileges);
        addResult.Should().BeTrue();

        // Act - Remove privileges
        var removeResult = TokenManipulator.RemovePrivileges(privileges);
        removeResult.Should().BeTrue();
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void AddPrivileges_WithNullArray_ShouldThrowArgumentNullException()
    {
        var act = () => TokenManipulator.AddPrivileges(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("privileges");
    }

    [Fact]
    public void RemovePrivileges_WithNullArray_ShouldThrowArgumentNullException()
    {
        var act = () => TokenManipulator.RemovePrivileges(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("privileges");
    }

    [Fact]
    public void AddPrivileges_WithWhitespacePrivilege_ShouldReturnFalse()
    {
        // Arrange & Act
        var result = TokenManipulator.AddPrivileges("   ");

        // Assert - Whitespace privilege should fail
        result.Should().BeFalse();
    }

    [Fact]
    public void AddPrivileges_WithEmptyStringPrivilege_ShouldReturnFalse()
    {
        // Arrange & Act
        var result = TokenManipulator.AddPrivileges("");

        // Assert - Empty privilege should fail
        result.Should().BeFalse();
    }

    #endregion
}
