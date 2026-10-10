using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ExceptionHelperTests
{
    [Fact]
    public void InvalidState_WithoutDetails_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.InvalidState();
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InvalidState_WithDetails_ShouldAppendDetails()
    {
        var ex = ExceptionHelper.InvalidState("custom detail");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain("custom detail");
    }

    [Fact]
    public void InvalidFileName_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.InvalidFileName("testParam");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("testParam");
    }

    [Fact]
    public void DangerousArguments_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.DangerousArguments("arg");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("arg");
    }

    [Fact]
    public void UnknownHive_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.UnknownHive("hive");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("hive");
    }

    [Fact]
    public void NoUpdatesAvailable_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.NoUpdatesAvailable();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void SetupFileUrlNotFound_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.SetupFileUrlNotFound();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void RGBKeyboardUnsupported_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.RGBKeyboardUnsupported();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void CantManageWithVantage_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.CantManageWithVantage();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void PowerModeNotSupported_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.PowerModeNotSupported();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void FanTableLength_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.FanTableLength("table");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("table");
    }

    [Fact]
    public void OptimizationActionNotFound_ShouldFormatActionKey()
    {
        var ex = ExceptionHelper.OptimizationActionNotFound("my-action");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain("my-action");
    }

    [Fact]
    public void OptimizationActionRollbackUnavailable_ShouldFormatActionKey()
    {
        var ex = ExceptionHelper.OptimizationActionRollbackUnavailable("rollback-action");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CommandExitedNonZero_ShouldFormatCommandAndExitCode()
    {
        var ex = ExceptionHelper.CommandExitedNonZero("test-cmd", 42, "error output");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain("test-cmd");
        ex.Message.Should().Contain("42");
    }

    [Fact]
    public void CommandCannotBeEmpty_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.CommandCannotBeEmpty("cmd");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("cmd");
    }

    [Fact]
    public void DeletionSystemPathsNotAllowed_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.DeletionSystemPathsNotAllowed();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void WildcardDeletionRestricted_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.WildcardDeletionRestricted();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void DeletionCriticalRegistryNotAllowed_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.DeletionCriticalRegistryNotAllowed();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void HandleInvalid_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.HandleInvalid();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void BuiltInDisplayNotFound_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.BuiltInDisplayNotFound();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void NoSupportedFeature_ShouldFormatTypeName()
    {
        var ex = ExceptionHelper.NoSupportedFeature("MyFeature");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain("MyFeature");
    }

    [Fact]
    public void IoCAlreadyInitialized_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.IoCAlreadyInitialized();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void IoCMustBeInitialized_ShouldFormatTypeName()
    {
        var ex = ExceptionHelper.IoCMustBeInitialized("SomeService");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain("SomeService");
    }

    [Fact]
    public void UnparseableVersionFormat_ShouldReturnFormatException()
    {
        var ex = ExceptionHelper.UnparseableVersionFormat("v1.2.3");
        ex.Should().BeOfType<FormatException>();
        ex.Message.Should().Contain("v1.2.3");
    }

    [Fact]
    public void GodModePresetNotFound_ShouldFormatPresetId()
    {
        var id = Guid.NewGuid();
        var ex = ExceptionHelper.GodModePresetNotFound(id);
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain(id.ToString());
    }

    [Fact]
    public void NoGodModePresetCreated_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.NoGodModePresetCreated();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void NoGodModePresetAvailable_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.NoGodModePresetAvailable();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void InvalidSettingsFilename_ShouldFormatFilenameAndParamName()
    {
        var ex = ExceptionHelper.InvalidSettingsFilename("bad.json", "filename");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("filename");
        ex.Message.Should().Contain("bad.json");
    }

    [Fact]
    public void TableDataCannotBeEmpty_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.TableDataCannotBeEmpty("data");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("data");
    }

    [Fact]
    public void TempArrayMustBe10_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.TempArrayMustBe10("arr");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("arr");
    }

    [Fact]
    public void TagNameNullOrEmpty_ShouldReturnArgumentExceptionWithParamName()
    {
        var ex = ExceptionHelper.TagNameNullOrEmpty("tag");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("tag");
    }

    [Fact]
    public void InteractiveShellRequiresArgs_ShouldReturnArgumentException()
    {
        var ex = ExceptionHelper.InteractiveShellRequiresArgs("shellArgs");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("shellArgs");
    }

    [Fact]
    public void PowerShellDangerousArgs_ShouldReturnArgumentException()
    {
        var ex = ExceptionHelper.PowerShellDangerousArgs("args");
        ex.Should().BeOfType<ArgumentException>();
        ex.ParamName.Should().Be("args");
    }

    [Fact]
    public void NoSupportedVersionFound_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.NoSupportedVersionFound();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void NoSupportedControllerFound_ShouldReturnInvalidOperationException()
    {
        var ex = ExceptionHelper.NoSupportedControllerFound();
        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void SettingsPathEscapesAllowedDir_ShouldFormatPath()
    {
        var ex = ExceptionHelper.SettingsPathEscapesAllowedDir("/bad/path");
        ex.Should().BeOfType<InvalidOperationException>();
        ex.Message.Should().Contain("/bad/path");
    }
}