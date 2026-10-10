using FluentAssertions;
using Microsoft.Win32;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class InstallationRegistryTransactionTests : IDisposable
{
    private readonly string _rootPath = @"Software\UniversalDeviceToolkit.Tests\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;

    public InstallationRegistryTransactionTests() => _root = Registry.CurrentUser.CreateSubKey(_rootPath);

    [Fact]
    public void Restore_PreservesCommonSubkeysAndRawExpandedValues()
    {
        using var view = _root.CreateSubKey("Native");
        var commonPath = InstallationRegistryTransaction.UninstallPath + "\\" + InstallationRegistryTransaction.ProductKeyName;
        using (var common = view.CreateSubKey(commonPath))
        {
            common.SetValue("Executable", @"%TEMP%\original.exe", RegistryValueKind.ExpandString);
            using var child = common.CreateSubKey(@"Nested\State");
            child.SetValue("Flags", new[] { "first", "second" }, RegistryValueKind.MultiString);
            child.SetValue("", new byte[] { 1, 2, 3 }, RegistryValueKind.Binary);
        }
        var transaction = new InstallationRegistryTransaction([view], @"C:\UDT");
        view.DeleteSubKeyTree(commonPath);
        using (var replacement = view.CreateSubKey(commonPath))
            replacement.SetValue("NewValue", "must disappear");

        transaction.Restore().Should().BeEmpty();

        using var restored = view.OpenSubKey(commonPath);
        restored.Should().NotBeNull();
        restored?.GetValue("Executable", null, RegistryValueOptions.DoNotExpandEnvironmentNames).Should().Be(@"%TEMP%\original.exe");
        restored?.GetValueKind("Executable").Should().Be(RegistryValueKind.ExpandString);
        restored?.GetValue("NewValue").Should().BeNull();
        using var nested = restored?.OpenSubKey(@"Nested\State");
        nested.Should().NotBeNull();
        nested?.GetValue("Flags").Should().BeEquivalentTo(new[] { "first", "second" });
        nested?.GetValue("").Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void DuplicateCleanup_UsesBothViewsAndOnlyCanonicalExactDestination()
    {
        using var nativeView = _root.CreateSubKey("Native");
        using var legacyView = _root.CreateSubKey("Legacy");
        WriteUninstallRecord(nativeView, "native-duplicate", @"C:\UDT\.");
        WriteUninstallRecord(legacyView, "legacy-duplicate", @"c:\UDT\child\..\");
        WriteUninstallRecord(legacyView, InstallationRegistryTransaction.ProductKeyName, @"C:\UDT");
        WriteUninstallRecord(legacyView, "different-install", @"C:\UDT-other");
        WriteUninstallRecord(legacyView, "different-product", @"C:\UDT", "Different Product");
        WriteUninstallRecord(legacyView, "malformed-location", "bad\0path");
        var transaction = new InstallationRegistryTransaction([nativeView, legacyView], @"C:\UDT");

        transaction.RemoveDuplicateRecords();

        AssertExists(nativeView, "native-duplicate", false);
        AssertExists(legacyView, "legacy-duplicate", false);
        AssertExists(legacyView, InstallationRegistryTransaction.ProductKeyName, false);
        AssertExists(legacyView, "different-install", true);
        AssertExists(legacyView, "different-product", true);
        AssertExists(legacyView, "malformed-location", true);

        transaction.Restore().Should().BeEmpty();
        AssertExists(nativeView, "native-duplicate", true);
        AssertExists(legacyView, "legacy-duplicate", true);
        AssertExists(legacyView, InstallationRegistryTransaction.ProductKeyName, true);
        using var nested = legacyView.OpenSubKey(InstallationRegistryTransaction.UninstallPath + @"\legacy-duplicate\Nested");
        nested?.GetValue("Version").Should().Be(12);
    }

    [Fact]
    public void Restore_RemovesCommonRegistrationCreatedAfterSnapshot()
    {
        using var view = _root.CreateSubKey("Native");
        var transaction = new InstallationRegistryTransaction([view], @"C:\UDT");
        var commonPath = InstallationRegistryTransaction.UninstallPath + "\\" + InstallationRegistryTransaction.ProductKeyName;
        using (var common = view.CreateSubKey(commonPath))
            common.SetValue("DisplayName", "Universal Device Toolkit");

        transaction.Restore().Should().BeEmpty();

        AssertExists(view, InstallationRegistryTransaction.ProductKeyName, false);
    }

    [Fact]
    public void DuplicateChangedAfterSnapshot_IsPreserved()
    {
        using var view = _root.CreateSubKey("Native");
        WriteUninstallRecord(view, "changed", @"C:\UDT");
        var transaction = new InstallationRegistryTransaction([view], @"C:\UDT");
        using (var changed = view.OpenSubKey(InstallationRegistryTransaction.UninstallPath + @"\changed", writable: true))
            changed?.SetValue("InstallLocation", @"C:\AnotherUDT");

        transaction.RemoveDuplicateRecords();
        transaction.Restore().Should().BeEmpty();

        using var record = view.OpenSubKey(InstallationRegistryTransaction.UninstallPath + @"\changed");
        record?.GetValue("InstallLocation").Should().Be(@"C:\AnotherUDT");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyCommonRecordChangedAfterSnapshot_IsNotRewrittenOnRollback(bool existed)
    {
        using var nativeView = _root.CreateSubKey("Native");
        using var legacyView = _root.CreateSubKey("Legacy");
        if (existed) WriteUninstallRecord(legacyView, InstallationRegistryTransaction.ProductKeyName, @"C:\UDT");
        var transaction = new InstallationRegistryTransaction([nativeView, legacyView], @"C:\UDT");
        WriteUninstallRecord(legacyView, InstallationRegistryTransaction.ProductKeyName, @"C:\AnotherUDT");

        transaction.RemoveDuplicateRecords();
        transaction.Restore().Should().BeEmpty();

        using var record = legacyView.OpenSubKey(InstallationRegistryTransaction.UninstallPath + "\\" + InstallationRegistryTransaction.ProductKeyName);
        record?.GetValue("InstallLocation").Should().Be(@"C:\AnotherUDT");
    }

    [Theory]
    [InlineData("\"C:\\UDT\\Uninstall Universal Device Toolkit.exe\" /allusers", null, true)]
    [InlineData("\"c:\\UDT\\.\\Uninstall.exe\" /currentuser", null, true)]
    [InlineData("\"C:\\UDT\\Uninstall.exe\"", null, true)]
    [InlineData("\"C:\\UDT\\Uninstall.exe\" /allusers", "C:\\AnotherUDT", false)]
    [InlineData("\"C:\\UDT\\Uninstall.exe\"", "", false)]
    [InlineData("C:\\UDT\\Uninstall.exe /allusers", null, false)]
    [InlineData("\"C:\\UDT\\Uninstall.exe\" /allusers /unexpected", null, false)]
    [InlineData("\"C:\\UDT-other\\Uninstall.exe\" /allusers", null, false)]
    [InlineData("\"C:\\UDT\\Some Other.exe\" /allusers", null, false)]
    [InlineData("\"UDT\\Uninstall.exe\" /allusers", null, false)]
    public void LegacyDuplicateCleanup_RequiresQuotedKnownUninstallerAtExactDestination(
        string command, string? explicitLocation, bool matches)
    {
        using var view = _root.CreateSubKey("Native");
        var recordPath = InstallationRegistryTransaction.UninstallPath + @"\legacy-electron";
        using (var record = view.CreateSubKey(recordPath))
        {
            record.SetValue("DisplayName", "Universal Device Toolkit");
            record.SetValue("UninstallString", command);
            if (explicitLocation != null) record.SetValue("InstallLocation", explicitLocation);
        }
        var transaction = new InstallationRegistryTransaction([view], @"C:\UDT");

        transaction.RemoveDuplicateRecords();

        AssertExists(view, "legacy-electron", !matches);
        transaction.Restore().Should().BeEmpty();
        using var restored = view.OpenSubKey(recordPath);
        restored?.GetValue("UninstallString").Should().Be(command);
        restored?.GetValue("InstallLocation").Should().Be(explicitLocation);
    }

    private static void WriteUninstallRecord(RegistryKey view, string name, string location,
        string displayName = "Universal Device Toolkit")
    {
        using var record = view.CreateSubKey(InstallationRegistryTransaction.UninstallPath + "\\" + name);
        record.SetValue("DisplayName", displayName);
        record.SetValue("InstallLocation", location);
        using var child = record.CreateSubKey("Nested");
        child.SetValue("Version", 12);
    }

    private static void AssertExists(RegistryKey view, string name, bool expected)
    {
        using var key = view.OpenSubKey(InstallationRegistryTransaction.UninstallPath + "\\" + name);
        (key != null).Should().Be(expected);
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_rootPath, throwOnMissingSubKey: false);
    }
}
