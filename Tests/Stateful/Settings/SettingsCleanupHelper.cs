using System;
using System.IO;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Tests.Settings
{
    /// <summary>
    /// Helper class for managing settings files during tests
    /// Prevents test pollution by cleaning up settings files
    /// </summary>
    public static class SettingsCleanupHelper
    {
        private static readonly object AppDataOverrideLock = new();
        private static string? _temporaryAppDataRoot;

        public static void UseIsolatedAppData()
        {
            lock (AppDataOverrideLock)
            {
                if (_temporaryAppDataRoot is not null)
                    return;

                _temporaryAppDataRoot = Path.Combine(Path.GetTempPath(), $"udt-settings-tests-{Guid.NewGuid():N}");
                Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _temporaryAppDataRoot);
                AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDeleteTemporaryAppDataRoot();
            }
        }

        /// <summary>
        /// Deletes a specific settings file
        /// </summary>
        public static void CleanupSettingsFile(string fileName)
        {
            try
            {
                UseIsolatedAppData();
                var filePath = Path.Combine(Folders.AppData, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        /// <summary>
        /// Common settings file names used in tests
        /// </summary>
        public static class SettingsFiles
        {
            public const string GodMode = "godmode.json";
            public const string RGBKeyboard = "rgb_keyboard.json";
            public const string GPUOverclock = "gpu_oc.json";
            public const string UpdateCheck = "update_check.json";
            public const string Application = "settings.json";
            public const string Integrations = "integrations.json";
            public const string BalanceMode = "balancemode.json";
            public const string SunriseSunset = "sunrise_sunset.json";
        }

        private static void TryDeleteTemporaryAppDataRoot()
        {
            try
            {
                if (_temporaryAppDataRoot is not null && Directory.Exists(_temporaryAppDataRoot))
                    Directory.Delete(_temporaryAppDataRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
