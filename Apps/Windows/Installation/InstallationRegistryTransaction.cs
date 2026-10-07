using Microsoft.Win32;

namespace UniversalDeviceToolkit.Windows;

internal sealed class InstallationRegistryTransaction
{
    internal const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    internal const string ProductKeyName = "UniversalDeviceToolkit";
    private readonly string _destination;
    private readonly List<RegistryEntry> _duplicates = [];
    private readonly List<RegistryEntry> _modified = [];

    internal InstallationRegistryTransaction(IEnumerable<RegistryKey> roots, string destination)
    {
        _destination = NormalizePath(destination);
        var firstRoot = true;
        foreach (var root in roots)
        {
            var canonicalRoot = firstRoot;
            firstRoot = false;
            var commonPath = UninstallPath + "\\" + ProductKeyName;
            if (canonicalRoot)
            {
                using var common = root.OpenSubKey(commonPath);
                _modified.Add(new RegistryEntry(root, commonPath, common == null ? null : KeySnapshot.Capture(common)));
            }
            using var uninstall = root.OpenSubKey(UninstallPath);
            if (uninstall == null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                if (canonicalRoot && name.Equals(ProductKeyName, StringComparison.OrdinalIgnoreCase)) continue;
                using var candidate = uninstall.OpenSubKey(name);
                if (candidate == null || !MatchesInstallation(candidate)) continue;
                _duplicates.Add(new RegistryEntry(root, UninstallPath + "\\" + name, KeySnapshot.Capture(candidate)));
            }
        }
    }

    internal void RemoveDuplicateRecords()
    {
        foreach (var entry in _duplicates)
        {
            using (var current = entry.Root.OpenSubKey(entry.Path))
            {
                if (current == null || !MatchesInstallation(current)) continue;
            }
            // Track before mutation so a partially failed deletion can be restored.
            if (!_modified.Any(modified => ReferenceEquals(modified.Root, entry.Root)
                && modified.Path.Equals(entry.Path, StringComparison.OrdinalIgnoreCase)))
                _modified.Add(entry);
            entry.Root.DeleteSubKeyTree(entry.Path, throwOnMissingSubKey: false);
        }
    }

    internal IReadOnlyList<Exception> Restore()
    {
        var failures = new List<Exception>();
        foreach (var entry in _modified)
        {
            try
            {
                entry.Root.DeleteSubKeyTree(entry.Path, throwOnMissingSubKey: false);
                if (entry.Snapshot == null) continue;
                using var restored = entry.Root.CreateSubKey(entry.Path);
                entry.Snapshot.WriteTo(restored);
            }
            catch (Exception error)
            {
                failures.Add(new IOException("Unable to restore installation registry key: " + entry.Path, error));
            }
        }
        return failures;
    }

    private bool MatchesInstallation(RegistryKey key)
    {
        if (key.GetValue("DisplayName") is not string display || display != "Universal Device Toolkit") return false;
        try
        {
            string location;
            if (key.GetValueNames().Contains("InstallLocation", StringComparer.OrdinalIgnoreCase))
            {
                if (key.GetValue("InstallLocation") is not string explicitLocation) return false;
                location = explicitLocation;
            }
            else
            {
                // Historical electron-builder records store the location only in a quoted uninstaller command.
                if (key.GetValue("UninstallString") is not string command || command.Length < 3 || command[0] != '"'
                    || command.Any(char.IsControl)) return false;
                var end = command.IndexOf('"', 1);
                if (end < 0) return false;
                var arguments = command[(end + 1)..].Trim();
                if (arguments.Length != 0 && !arguments.Equals("/allusers", StringComparison.OrdinalIgnoreCase)
                    && !arguments.Equals("/currentuser", StringComparison.OrdinalIgnoreCase)) return false;
                var executable = command[1..end];
                var name = Path.GetFileName(executable);
                if (!Path.IsPathFullyQualified(executable)
                    || (!name.Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase)
                        && !name.Equals("Uninstall Universal Device Toolkit.exe", StringComparison.OrdinalIgnoreCase))) return false;
                location = Path.GetDirectoryName(executable) ?? "";
            }
            if (!Path.IsPathFullyQualified(location)) return false;
            return NormalizePath(location).Equals(_destination, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed record RegistryEntry(RegistryKey Root, string Path, KeySnapshot? Snapshot);
    private sealed record ValueSnapshot(object Value, RegistryValueKind Kind);

    private sealed class KeySnapshot
    {
        private readonly Dictionary<string, ValueSnapshot> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, KeySnapshot> _children = new(StringComparer.OrdinalIgnoreCase);

        internal static KeySnapshot Capture(RegistryKey key)
        {
            var snapshot = new KeySnapshot();
            foreach (var name in key.GetValueNames())
            {
                var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                    ?? throw new IOException("Unable to snapshot installation registry value: " + name);
                snapshot._values.Add(name, new ValueSnapshot(value, key.GetValueKind(name)));
            }
            foreach (var name in key.GetSubKeyNames())
            {
                using var child = key.OpenSubKey(name)
                    ?? throw new IOException("Unable to snapshot installation registry subkey: " + name);
                snapshot._children.Add(name, Capture(child));
            }
            return snapshot;
        }

        internal void WriteTo(RegistryKey key)
        {
            foreach (var (name, value) in _values)
                key.SetValue(name, value.Value, value.Kind);
            foreach (var (name, snapshot) in _children)
            {
                using var child = key.CreateSubKey(name);
                snapshot.WriteTo(child);
            }
        }
    }
}
