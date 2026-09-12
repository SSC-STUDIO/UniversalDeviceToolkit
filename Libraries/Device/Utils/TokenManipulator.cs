using System;
using System.Linq;
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Lib.Extensions;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Security;

namespace UniversalDeviceToolkit.Lib.Utils;

public static class TokenManipulator
{
    private const int ErrorNotAllAssigned = 1300;

    public const string SE_BACKUP_PRIVILEGE = "SeBackupPrivilege";
    public const string SE_RESTORE_PRIVILEGE = "SeRestorePrivilege";
    public const string SE_TAKE_OWNERSHIP_PRIVILEGE = "SeTakeOwnershipPrivilege";
    public const string SE_SYSTEM_ENVIRONMENT_PRIVILEGE = "SeSystemEnvironmentPrivilege";

    public static bool AddPrivileges(params string[] privileges) => AdjustPrivileges(privileges, true);

    public static bool RemovePrivileges(params string[] privileges) => AdjustPrivileges(privileges, false);

    private static bool AdjustPrivileges(string[] privileges, bool enable)
    {
        if (privileges is null)
            throw new ArgumentNullException(nameof(privileges));

        if (privileges.Length == 0)
            return true;

        if (privileges.Any(string.IsNullOrWhiteSpace))
            return false;

        SafeProcessHandle? safeHandle = null;
        SafeFileHandle? safeTokenHandle = null;

        try
        {
            safeHandle = new SafeProcessHandle(PInvoke.GetCurrentProcess(), false);
            if (!PInvoke.OpenProcessToken(safeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out safeTokenHandle)
                || safeTokenHandle is null
                || safeTokenHandle.IsInvalid)
                return false;

            return privileges.All(p => AdjustPrivilege(safeTokenHandle, p, enable));
        }
        finally
        {
            safeTokenHandle?.Dispose();
            safeHandle?.Dispose();
        }
    }

    private static unsafe bool AdjustPrivilege(SafeFileHandle safeTokenHandle, string privilegeName, bool enable)
    {
        if (!PInvoke.LookupPrivilegeValue(null, privilegeName, out var luid))
            return false;

        var state = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
        state.Privileges[0] = new LUID_AND_ATTRIBUTES
        {
            Luid = luid,
            Attributes = enable ? TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED : 0
        };

        if (!PInvoke.AdjustTokenPrivileges(safeTokenHandle.ToWin32Handle(), false, &state, 0, null, null))
            return false;

        return Marshal.GetLastWin32Error() != ErrorNotAllAssigned;
    }
}
