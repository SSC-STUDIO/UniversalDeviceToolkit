using System.IO;
using SharedFolders = UniversalDeviceToolkit.Shared.Utils.Folders;

namespace UniversalDeviceToolkit.Lib.Utils;

// Thin ABI-compatible wrapper over UniversalDeviceToolkit.Shared.Utils.Folders
// (single source of truth). Behavior on Windows is identical to the legacy
// implementation: same AppData root (%LOCALAPPDATA%\UniversalDeviceToolkit),
// same one-shot legacy migration, same XDG handling only on non-Windows
// platforms that Lib never targets.
public static class Folders
{
    public static string AppDataOverrideEnvironmentVariable => SharedFolders.AppDataOverrideEnvironmentVariable;

    public static string Program => SharedFolders.Program;

    public static string LegacyAppData => SharedFolders.LegacyAppData;

    public static string AppData => SharedFolders.AppData;

    public static string GetAppDataSubdirectory(string subdirectory) => SharedFolders.GetAppDataSubdirectory(subdirectory);

    public static string Temp => SharedFolders.Temp;

    // NOTE: internal Folders.TryCopyMissingDirectoryEntries no longer exists in
    // Lib — the implementation lives in the Shared assembly. No Lib callers used
    // it outside this file (verified by audit).
}
