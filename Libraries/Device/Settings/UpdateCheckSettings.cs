using System;

namespace UniversalDeviceToolkit.Lib.Settings;

public class UpdateCheckSettings() : AbstractSettings<UpdateCheckSettings.UpdateCheckSettingsStore>("update_check.json")
{
    public class UpdateCheckSettingsStore
    {
        public DateTime? LastUpdateCheckDateTime { get; set; }
        public UpdateCheckFrequency UpdateCheckFrequency { get; set; }

        /// <summary>
        /// When false (default), auto-update only offers stable releases.
        /// When true, prerelease/preview releases are offered as well.
        /// </summary>
        public bool IncludePrereleaseUpdates { get; set; }

        // SECURITY: These settings are persisted to a JSON file in %APPDATA% and can be tampered with.
        // In RELEASE builds, UpdateChecker ignores these values and always uses the hardcoded AppIdentity defaults.
        // Custom repositories are only respected in DEBUG builds, and the repository owner must match
        // the TrustedRepositoryOwners allowlist in UpdateChecker.cs.
        public string? UpdateRepositoryOwner { get; set; }
        public string? UpdateRepositoryName { get; set; }
    }

    protected override UpdateCheckSettingsStore Default => new()
    {
        LastUpdateCheckDateTime = null,
        UpdateCheckFrequency = UpdateCheckFrequency.PerDay,
        UpdateRepositoryOwner = null, // null means use default
        UpdateRepositoryName = null // null means use default
    };
}
