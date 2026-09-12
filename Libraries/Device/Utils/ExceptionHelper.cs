using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Lib.Resources;
#if WINDOWS
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
#endif

namespace UniversalDeviceToolkit.Lib.Utils;

// Windows-specific factories remain here because they use localized Windows
// resource strings. The shared settings factories are forwarded below so their
// validation behavior has one implementation.
using SharedExceptionHelper = UniversalDeviceToolkit.Shared.Utils.ExceptionHelper;
public static class ExceptionHelper
{
    public static InvalidOperationException InvalidState(string? details = null) =>
        new(details is null ? Resource.Exception_InvalidState : $"{Resource.Exception_InvalidState}: {details}");

    public static ArgumentException InvalidFileName(string paramName) =>
        new(Resource.Exception_InvalidFileName, paramName);

    public static ArgumentException DangerousArguments(string paramName) =>
        new(Resource.Exception_DangerousArguments, paramName);

    public static ArgumentException InteractiveShellRequiresArgs(string paramName) =>
        new(Resource.Exception_InteractiveShellRequiresArgs, paramName);

    public static ArgumentException PowerShellDangerousArgs(string paramName) =>
        new(Resource.Exception_PowerShellDangerousArgs, paramName);

    public static ArgumentException UnknownHive(string paramName) =>
        new(Resource.Exception_UnknownHive, paramName);

    public static InvalidOperationException NoUpdatesAvailable() =>
        new(Resource.Exception_NoUpdatesAvailable);

    public static InvalidOperationException SetupFileUrlNotFound() =>
        new(Resource.Exception_SetupFileUrlNotFound);

    public static InvalidOperationException RGBKeyboardUnsupported() =>
        new(Resource.Exception_RGBKeyboardUnsupported);

    public static InvalidOperationException CantManageWithVantage() =>
        new(Resource.Exception_CantManageWithVantage);

    public static InvalidOperationException PowerModeNotSupported() =>
        new(Resource.Exception_PowerModeNotSupported);

    public static InvalidOperationException CannotSetUEFIPrivileges() =>
        new(Resource.Exception_CannotSetUEFIPrivileges);

    public static InvalidOperationException FailedToStartExplorer() =>
        new(Resource.Exception_FailedToStartExplorer);

    public static TimeoutException ExplorerDidNotRestart() =>
        new(Resource.Exception_ExplorerDidNotRestart);

    public static TimeoutException ExplorerDidNotExit() =>
        new(Resource.Exception_ExplorerDidNotExit);

    public static ArgumentException InvalidActionKey(string paramName) =>
        new(Resource.Exception_InvalidActionKey, paramName);

    public static InvalidOperationException OptimizationActionNotFound(string actionKey) =>
        new(string.Format(Resource.Exception_OptimizationActionNotFound, actionKey));

    public static InvalidOperationException OptimizationActionRollbackUnavailable(string actionKey) =>
        new(string.Format(Resource.Exception_OptimizationActionRollbackUnavailable, actionKey));

    public static InvalidOperationException CommandExitedNonZero(string command, int exitCode, string errorOutput) =>
        new(string.Format(Resource.Exception_CommandExitedNonZero, command, exitCode, errorOutput));

    public static ArgumentException CommandCannotBeEmpty(string paramName) =>
        new(Resource.Exception_CommandCannotBeEmpty, paramName);

    public static InvalidOperationException DeletionSystemPathsNotAllowed() =>
        new(Resource.Exception_DeletionSystemPathsNotAllowed);

    public static InvalidOperationException WildcardDeletionRestricted() =>
        new(Resource.Exception_WildcardDeletionRestricted);

    public static InvalidOperationException DeletionCriticalRegistryNotAllowed() =>
        new(Resource.Exception_DeletionCriticalRegistryNotAllowed);

    public static InvalidOperationException CurrentUserValueNull() =>
        new(Resource.Exception_CurrentUserValueNull);

    public static InvalidOperationException HandleInvalid() =>
        new(Resource.Exception_HandleInvalid);

    public static InvalidOperationException FailedToDeserializeJSON() =>
        new(Resource.Exception_FailedToDeserializeJSON);

    public static InvalidOperationException FeatureNotSupported() =>
        new(Resource.Exception_NoSupportedFeature);

    public static InvalidOperationException NoSupportedFeature(string typeName) =>
        new($"{Resource.Exception_NoSupportedFeature} [type={typeName}]");

    public static InvalidOperationException BuiltInDisplayNotFound() =>
        new(Resource.Exception_BuiltInDisplayNotFound);

    public static InvalidOperationException FailedToDeserializeProfile() =>
        new(Resource.Exception_FailedToDeserializeProfile);

    public static InvalidOperationException BrightnessRange() =>
        new(Resource.Exception_BrightnessRange);

    public static InvalidOperationException ProfileRange() =>
        new(Resource.Exception_ProfileRange);

    public static ArgumentException TableDataCannotBeEmpty(string paramName) =>
        new(Resource.Exception_TableDataCannotBeEmpty, paramName);

    public static ArgumentException TempArrayMustBe10(string paramName) =>
        new(Resource.Exception_TempArrayMustBe10, paramName);

    public static InvalidDataException FileChecksumMismatch() =>
        new(Resource.Exception_FileChecksumMismatch);

    public static InvalidOperationException DevicePropertyNotGUID() =>
        new(Resource.Exception_DevicePropertyNotGUID);

    public static InvalidOperationException DevicePropertyNotString() =>
        new(Resource.Exception_DevicePropertyNotString);

    public static ArgumentException OffsetCountExceedArray() =>
        new(Resource.Exception_OffsetCountExceedArray);

    public static FileNotFoundException ProfileFileNotFound(string path) =>
        new(Resource.Exception_ProfileFileNotFound, path);

    public static ExternalException OpenServiceError() =>
        new(Resource.Exception_OpenServiceError);

    public static ExternalException OpenServiceManagerError() =>
        new(Resource.Exception_OpenServiceManagerError);

    public static InvalidDataException ResourceCatalogEmpty() =>
        new(Resource.Exception_ResourceCatalogEmpty);

    public static InvalidOperationException CouldNotDeserializeEffects() =>
        new(Resource.Exception_CouldNotDeserializeEffects);

    public static InvalidOperationException CannotReadUEFIVariable(string variableName) =>
        new(string.Format(Resource.Exception_CannotReadVariableUEFI, variableName));

    public static InvalidOperationException CannotWriteUEFIVariable(string variableName) =>
        new(string.Format(Resource.Exception_CannotWriteVariableUEFI, variableName));

    public static InvalidOperationException WmiFeatureUnavailable(object capabilityId, Exception inner) =>
        new(string.Format(Resource.Exception_WmiFeatureUnavailable, capabilityId), inner);

    public static InvalidOperationException UndefinedValueReceived(object value) =>
        new(string.Format(Resource.Exception_UndefinedValueReceived, value));

    public static InvalidOperationException UnsupportedPowerMode(object mode) =>
        new(string.Format(Resource.Exception_UnsupportedPowerMode, mode));

    public static InvalidOperationException CommandFailedSecurity(string command) =>
        new(string.Format(Resource.Exception_CommandFailedSecurity, command));

    public static InvalidOperationException NotInAllowlist(string fileName) =>
        new(string.Format(Resource.Exception_NotInAllowlist, fileName));

    public static InvalidOperationException DangerousPatternInArgs(string fileName) =>
        new(string.Format(Resource.Exception_DangerousPatternInArgs, fileName));

    public static InvalidDataException DevicePackUnsupportedFileType(string entryName) =>
        new(string.Format(Resource.Exception_DevicePackUnsupportedFileType, entryName));

    public static InvalidDataException DevicePackUnsafePath(string entryName) =>
        new(string.Format(Resource.Exception_DevicePackUnsafePath, entryName));

    public static InvalidDataException DevicePackWindowsPathSep(string entryName) =>
        new(string.Format(Resource.Exception_DevicePackWindowsPathSep, entryName));

    public static InvalidDataException DevicePackNotAvailable(string packId) =>
        new(string.Format(Resource.Exception_DevicePackNotAvailable, packId));

    public static InvalidDataException DevicePackNoManifest(string packId, string manifestFileName) =>
        new(string.Format(Resource.Exception_DevicePackNoManifest, packId, manifestFileName));

    public static InvalidDataException DevicePackManifestEmpty(string packId) =>
        new(string.Format(Resource.Exception_DevicePackManifestEmpty, packId));

    public static InvalidDataException DevicePackIdMismatch(string expectedId, string actualId) =>
        new(string.Format(Resource.Exception_DevicePackIdMismatch, expectedId, actualId));

    public static InvalidDataException DevicePackVendorMismatch(string expected, string actual) =>
        new(string.Format(Resource.Exception_DevicePackVendorMismatch, expected, actual));

    public static InvalidDataException DevicePackUrlEmpty(string packId) =>
        new(string.Format(Resource.Exception_DevicePackUrlEmpty, packId));

    public static InvalidDataException DevicePackSha256Missing(string packId) =>
        new(string.Format(Resource.Exception_DevicePackSha256Missing, packId));

    public static InvalidOperationException UnknownBatteryState(object state, string bits) =>
        new(string.Format(Resource.Exception_UnknownBatteryState, $"{state} [bits={bits}]"));

    public static InvalidOperationException DeviceHandleNotAvailable() =>
        new(Resource.Exception_DeviceHandleNotAvailable);

    public static InvalidOperationException MainModuleNull() =>
        new(Resource.Exception_MainModuleNull);

    public static InvalidOperationException CurrentProcessFileNameNull() =>
        new(Resource.Exception_CurrentProcessFileNameNull);

    public static InvalidOperationException CurrentProcessFileVersionNull() =>
        new(Resource.Exception_CurrentProcessFileVersionNull);

    public static InvalidDataException SHA256ValidationFailed(string expected, string actual) =>
        new(string.Format(Resource.Exception_SHA256ValidationFailed, expected, actual));

    public static InvalidOperationException IoCAlreadyInitialized() =>
        new(Resource.Exception_IoCAlreadyInitialized);

    public static InvalidOperationException IoCMustBeInitialized(string typeName) =>
        new(string.Format(Resource.Exception_IoCMustBeInitialized, typeName));

    public static ArgumentException FanTableLength(string paramName) =>
        new(Resource.Exception_FanTableLength, paramName);

    public static ArgumentException TagNameNullOrEmpty(string paramName) =>
        new(Resource.Exception_TagNameNullOrEmpty, paramName);

    public static FormatException UnparseableVersionFormat(string tagName) =>
        new(string.Format(Resource.Exception_UnparseableVersionFormat, tagName));

    public static InvalidOperationException GodModePresetNotFound(object presetId) =>
        new(string.Format(Resource.Exception_GodModePresetNotFound, presetId));

    public static InvalidOperationException NoGodModePresetCreated() =>
        new(Resource.Exception_NoGodModePresetCreated);

    public static InvalidOperationException NoGodModePresetAvailable() =>
        new(Resource.Exception_NoGodModePresetAvailable);

    public static InvalidOperationException NoSupportedVersionFound() =>
        new(Resource.Exception_NoSupportedVersionFound);

    public static InvalidOperationException NoSupportedControllerFound() =>
        new(Resource.Exception_NoSupportedControllerFound);

    public static ArgumentException InvalidSettingsFilename(string filename, string paramName) =>
        SharedExceptionHelper.InvalidSettingsFilename(filename, paramName);

    public static InvalidOperationException SettingsPathEscapesAllowedDir(string settingsStorePath) =>
        SharedExceptionHelper.SettingsPathEscapesAllowedDir(settingsStorePath);

#if WINDOWS
    public static SoftwareDisablerException FailedToRegisterTask(string taskName, string taskPath, string typeName, Exception inner) =>
        new(string.Format(Resource.Exception_FailedToRegisterTask, taskName, taskPath, typeName), inner);
#endif

    public static InvalidOperationException CouldNotReadRegistrySetting(string keyName) =>
        new(string.Format(Resource.Exception_CouldNotReadRegistrySetting, keyName));

    public static Win32Exception CouldNotGetColor(string colorName) =>
        new(string.Format(Resource.Exception_CouldNotGetColor, colorName));

    public static InvalidOperationException DriverHandleFailed(string driverPath) =>
        new(string.Format(Resource.Exception_DriverHandleFailed, driverPath));

    public static InvalidOperationException DriverHandleError(string driverPath, Exception inner) =>
        new(string.Format(Resource.Exception_DriverHandleError, driverPath), inner);

    public static InvalidOperationException DeviceIoControlErrorWithCode(int error) =>
        new(string.Format(Resource.Exception_DeviceIoControlErrorWithCode, error));

    public static InvalidOperationException DriverCommandError(uint controlCode, Exception inner) =>
        new(string.Format(Resource.Exception_DriverCommandError, controlCode), inner);

    // Use InvalidOperationException (not ManagementException) for wrapped WMI failures so:
    // 1) We do not re-fire "break when thrown" for ManagementException on every soft probe.
    // 2) Callers can catch Exception / InvalidOperationException without mistaking firmware probes
    //    for unhandled COM failures.
    public static InvalidOperationException WmiClassNotAvailable(string scope, string queryFormatted, Exception inner) =>
        new(string.Format(Resource.Exception_WmiClassNotAvailable, scope, queryFormatted), inner);

    public static InvalidOperationException WmiReadFailed(string message, string scope, FormattableString query, Exception inner) =>
        new(string.Format(Resource.Exception_WmiReadFailed, message, scope, query), inner);

    public static InvalidOperationException WmiCallFailed(string message, string scope, FormattableString query, string methodName, Exception inner) =>
        new(string.Format(Resource.Exception_WmiCallFailed, message, scope, query, methodName), inner);

    public static InvalidOperationException WmiCallFailedDot(string message, string scope, FormattableString query, string methodName, Exception inner) =>
        new(string.Format(Resource.Exception_WmiCallFailedDot, message, scope, query, methodName), inner);

    public static InvalidOperationException WmiCallFailedFormatted(string message, string scope, string queryFormatted, string methodName, Exception inner) =>
        new(string.Format(Resource.Exception_WmiCallFailed, message, scope, queryFormatted, methodName), inner);

    public static InvalidOperationException WmiNoResults() =>
        new(Resource.Exception_WmiNoResults);

    public static InvalidOperationException InvalidTypeOfFormatted() =>
        new(Resource.Exception_InvalidTypeOfFormatted);

    public static InvalidDataException UpdateNoSHA256Hash(string version) =>
        new(string.Format(Resource.Exception_UpdateNoSHA256Hash, version));

    public static InvalidDataException UpdateSHA256Mismatch(string expectedHash, string computedHash) =>
        new(string.Format(Resource.Exception_UpdateSHA256Mismatch, expectedHash, computedHash));
}
