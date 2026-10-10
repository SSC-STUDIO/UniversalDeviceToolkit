using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;

// ReSharper disable StringLiteralTypo

namespace UniversalDeviceToolkit.Lib.System;

public static class Registry
{
    public static IAsyncDisposable ObserveKey(string hive, string subKey, bool includeSubtreeChanges, Action handler)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var task = Task.Run(() => Handler(cancellationTokenSource.Token));

        return new LambdaAsyncDisposable(async () =>
        {
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            await task.ConfigureAwait(false);
            cancellationTokenSource.Dispose();
        });

        void Handler(CancellationToken token)
        {
            try
            {
                using var baseKey = GetBaseKey(hive);
                using var key = baseKey.OpenSubKey(subKey) ?? throw new InvalidOperationException(string.Format(Resource.Exception_KeyCouldNotBeOpened, subKey));

                using var resetEvent = new ManualResetEvent(false);

                while (true)
                {
                    var regNotifyChangeKeyValueResult = PInvoke.RegNotifyChangeKeyValue(key.Handle,
                        includeSubtreeChanges,
                        REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC,
                        resetEvent.SafeWaitHandle,
                        true);
                    if (regNotifyChangeKeyValueResult != WIN32_ERROR.NO_ERROR)
                        PInvokeExtensions.ThrowIfWin32Error("RegNotifyChangeKeyValue");

                    WaitHandle.WaitAny([resetEvent, token.WaitHandle]);
                    token.ThrowIfCancellationRequested();

                    handler();

                    resetEvent.Reset();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when registry monitoring is cancelled, no action needed
            }
            catch (ThreadAbortException)
            {
                // Expected when thread is aborted, no action needed
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Unknown error.", ex);
            }
        }
    }

    public static IDisposable ObserveValue(string hive, string path, string valueName, Action handler)
    {
        if (hive is "HKCU")
            hive = "HKEY_CURRENT_USER";

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting Win32 registry listener... [hive={hive}, path={path}, key={valueName}]");

        var cancellationTokenSource = new CancellationTokenSource();
        var task = Task.Run(() =>
        {
            try
            {
                using var baseKey = GetBaseKey(hive);
                using var key = baseKey.OpenSubKey(path) ?? throw new InvalidOperationException(string.Format(Resource.Exception_KeyCouldNotBeOpened, path));

                using var resetEvent = new ManualResetEvent(false);

                while (true)
                {
                    var regNotifyChangeKeyValueResult = PInvoke.RegNotifyChangeKeyValue(key.Handle,
                        false,
                        REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC,
                        resetEvent.SafeWaitHandle,
                        true);
                    if (regNotifyChangeKeyValueResult != WIN32_ERROR.NO_ERROR)
                        PInvokeExtensions.ThrowIfWin32Error("RegNotifyChangeKeyValue");

                    WaitHandle.WaitAny([resetEvent, cancellationTokenSource.Token.WaitHandle]);
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Win32 registry event arrived [hive={hive}, path={path}, key={valueName}]");

                    handler();

                    resetEvent.Reset();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when registry monitoring is cancelled, no action needed
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error in Win32 registry listener [hive={hive}, path={path}, key={valueName}].", ex);
            }
        }, cancellationTokenSource.Token);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started Win32 registry listener [hive={hive}, path={path}, key={valueName}]");

        return new LambdaDisposable(() =>
        {
           cancellationTokenSource.Cancel();
            try { task.Wait(1000); }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Win32 registry listener dispose wait failed.", ex);
            }
           cancellationTokenSource.Dispose();
        });
    }

    public static bool KeyExists(string hive, string subKey)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var registryKey = baseKey.OpenSubKey(subKey);
            return registryKey is not null;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"registry-key-exists-{hive}-{subKey}",
                $"Registry KeyExists failed for {hive}\\{subKey}.",
                ex);
            return false;
        }
    }

    public static bool ValueExists(string hive, string subKey, string valueName)
    {
        try
        {
            var keyName = Path.Combine(hive, subKey);
            var value = Microsoft.Win32.Registry.GetValue(keyName, valueName, null);
            return value is not null;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"registry-value-exists-{hive}-{subKey}-{valueName}",
                $"Registry ValueExists failed for {hive}\\{subKey}\\{valueName}.",
                ex);
            return false;
        }
    }

    public static string[] GetSubKeys(string hive, string subKey)
    {
        using var baseKey = GetBaseKey(hive);
        using var subKeyObj = baseKey.OpenSubKey(subKey);
        return subKeyObj?.GetSubKeyNames().Select(s => Path.Combine(subKey, s)).ToArray() ?? [];
    }

    public static T GetValue<T>(string hive, string subKey, string valueName, T defaultValue, bool doNotExpand = false)
    {
        using var baseKey = GetBaseKey(hive);
        using var subKeyObj = baseKey.OpenSubKey(subKey);
        var value = subKeyObj?.GetValue(valueName, defaultValue, doNotExpand ? RegistryValueOptions.DoNotExpandEnvironmentNames : RegistryValueOptions.None);

        if (value is not T t)
            return defaultValue;

        return t;
    }

    public static void SetValue<T>(string hive, string subKey, string valueName, T value, bool fixPermissions = false, RegistryValueKind valueKind = RegistryValueKind.Unknown) where T : notnull
    {
        try
        {
            Microsoft.Win32.Registry.SetValue(@$"{hive}\{subKey}", valueName, value, valueKind);
        }
        catch (UnauthorizedAccessException)
        {
            // AddPermissions used to restore the DACL in finally *before* the retry write,
            // so elevation never covered the actual SetValue. Perform the write while elevated.
            if (!fixPermissions || !TrySetValueWithElevatedPermissions(hive, subKey, valueName, value, valueKind))
                throw;
        }
    }

    public static void DeleteValue(string hive, string subKey, string valueName, bool fixPermissions = false)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch (UnauthorizedAccessException)
        {
            if (!fixPermissions || !WithElevatedPermissions(hive, subKey, () =>
                {
                    using var baseKey = GetBaseKey(hive);
                    using var key = baseKey.OpenSubKey(subKey, writable: true);
                    key?.DeleteValue(valueName, throwOnMissingValue: false);
                }))
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Take ownership + grant FullControl, write the value, then restore DACL/owner.
    /// </summary>
    private static bool TrySetValueWithElevatedPermissions<T>(
        string hive,
        string subKey,
        string valueName,
        T value,
        RegistryValueKind valueKind) where T : notnull
    {
        return WithElevatedPermissions(hive, subKey, () =>
        {
            Microsoft.Win32.Registry.SetValue(@$"{hive}\{subKey}", valueName, value, valueKind);
        });
    }

    public static void Delete(string hive, string subKey)
    {
        using var baseKey = GetBaseKey(hive);
        using var key = baseKey.OpenSubKey(subKey);
        if (key is null)
            return;
        baseKey.DeleteSubKeyTree(subKey);
    }

    /// <summary>
    /// Elevates registry key ACLs long enough to run <paramref name="action"/>, then restores
    /// the original DACL and owner. Returns false if elevation could not be established.
    /// </summary>
    private static bool WithElevatedPermissions(string hive, string subKey, Action action)
    {
        IdentityReference? originalOwner = null;
        RegistrySecurity? originalSecurity = null;
        var elevated = false;

        try
        {
            var current = WindowsIdentity.GetCurrent();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (current is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Could not get current user.");

                return false;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Attempting to elevate permissions on {hive}\\{subKey} for {current.Name}...");

            var user = current.User;
            if (user is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Could not get current security identifier of user {current.Name}.");

                return false;
            }

            if (!TakeOwnership(hive, subKey, user, out originalOwner))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Could not take ownership of {hive}\\{subKey}. [user={user}, originalOwner={originalOwner}]");

                return false;
            }

            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadKey);
            if (key is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to open key {hive}\\{subKey} for {current.Name}.");

                return false;
            }

            originalSecurity = key.GetAccessControl();
            var accessControl = key.GetAccessControl();

            const RegistryRights RIGHTS = RegistryRights.FullControl;
            const AccessControlType TYPE = AccessControlType.Allow;
            accessControl.AddAccessRule(new(user, RIGHTS, TYPE));
            key.SetAccessControl(accessControl);
            elevated = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Permissions elevated on {hive}\\{subKey} for {current.Name}. [rights={RIGHTS}, type={TYPE}]");

            action();
            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed elevated registry operation on {hive}\\{subKey}.", ex);

            if (!elevated)
                return false;

            throw;
        }
        finally
        {
            if (originalSecurity is not null)
            {
                try
                {
                    using var baseKey = GetBaseKey(hive);
                    using var restoreKey = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadKey);
                    if (restoreKey is not null)
                    {
                        restoreKey.SetAccessControl(originalSecurity);

                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Restored original DACL on {hive}\\{subKey}.");
                    }
                    else if (Log.Instance.IsTraceEnabled)
                    {
                        Log.Instance.Trace($"Could not open {hive}\\{subKey} to restore original DACL.");
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to restore original DACL on {hive}\\{subKey}.", ex);
                }
            }

            if (originalOwner is not null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Restoring ownership of {hive}\\{subKey} to {originalOwner}...");

                if (TakeOwnership(hive, subKey, originalOwner, out _))
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ownership of {hive}\\{subKey} restored to {originalOwner}.");
                }
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ownership of {hive}\\{subKey} NOT restored {originalOwner}.");
                }
            }
        }
    }

    private static bool TakeOwnership(string hive, string subKey, IdentityReference reference, out IdentityReference? previousIdentityReference)
    {
        previousIdentityReference = null;

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE, TokenManipulator.SE_RESTORE_PRIVILEGE, TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE))
                return false;

            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership);
            if (key is null)
                return false;

            var accessControl = key.GetAccessControl();

            previousIdentityReference = accessControl.GetOwner(typeof(NTAccount));
            if (previousIdentityReference is null)
                return false;

            accessControl.SetOwner(reference);
            key.SetAccessControl(accessControl);

            return true;
        }
        finally
        {
            _ = TokenManipulator.RemovePrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE, TokenManipulator.SE_RESTORE_PRIVILEGE, TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE);
        }
    }

    private static RegistryKey GetBaseKey(string hive) => hive switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => Microsoft.Win32.Registry.LocalMachine,
        "HKCU" or "HKEY_CURRENT_USER" => Microsoft.Win32.Registry.CurrentUser,
        "HKU" or "HKEY_USERS" => Microsoft.Win32.Registry.Users,
        "HKCR" or "HKEY_CLASSES_ROOT" => Microsoft.Win32.Registry.ClassesRoot,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Microsoft.Win32.Registry.CurrentConfig,
        _ => throw ExceptionHelper.UnknownHive(nameof(hive))
    };
}
