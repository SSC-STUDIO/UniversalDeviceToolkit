using System.Text.RegularExpressions;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class TestLayoutGuardTests
{
    private static readonly Regex FileScopedNamespace = new(
        @"(?m)^namespace\s+(?<name>[A-Za-z0-9_.]+)\s*;",
        RegexOptions.Compiled);

    private static readonly Regex BlockNamespace = new(
        @"(?m)^namespace\s+(?<name>[A-Za-z0-9_.]+)\s*\{",
        RegexOptions.Compiled);

    private static readonly Regex TypeDeclaration = new(
        @"(?m)\b(?:class|struct|record|interface)\s+(?<name>[A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly string[] TestProjectFolders =
    [
        "Tests/Unit",
        "Tests/Contracts",
        "Tests/Stateful",
        "Tests/Fast",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> AggregateTypeContracts =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Hardware/FanTableInfoTests.cs"] = ["FanTableInfoStructTests"],
            ["Automation/MacroTests.cs"] = [
                "MacroEventTests", "MacroIdentifierTests", "MacroSequenceTests",
                "MacroControllerCleanUpTests", "MacroControllerEnabledTests", "MacroControllerAllowedKeysTests"],
            ["Optimization/WindowsOptimizationRollbackTests.cs"] = [
                "WindowsOptimizationActionDefinitionContractTests",
                "WindowsOptimizationActionDefinitionSnapshotTests"],
            ["Features/FeatureTests.cs"] = [
                "IFeatureTests", "BatteryStateTests", "PowerModeStateTests", "HybridModeStateTests",
                "GPUStateTests", "FanTableTypeTests"],
            ["Settings/MoreSettingsStoreTests.cs"] = [
                "FanCurveSettingsStoreTests", "SpectrumKeyboardSettingsStoreTests"],
            ["Settings/PluginInfrastructureTests.cs"] = [
                "PluginManifestAdapterTests", "TestDataGeneratorTests", "AsyncTestHelpersTests",
                "TestAssertionsTests", "MockFactoryTests"],
            ["Settings/SettingsTestCollection.cs"] = [
                "LocalizationTestCollectionDefinition", "SettingsTestCollectionDefinition",
                "ProcessStateTestCollectionDefinition"],
            ["Utils/ThrottleDispatcherEdgeCaseTests.cs"] = [
                "ThrottleFirstDispatcherEdgeCaseTests", "ThrottleLastDispatcherEdgeCaseTests"]
        };

    [Fact]
    public void TestFiles_ShouldMatchDirectoryNamespaceAndTypeName()
    {
        var repo = RepositoryPaths.FindRoot();
        var failures = new List<string>();

        foreach (var folder in TestProjectFolders)
        {
            var root = Path.Combine(repo, folder);
            Directory.Exists(root).Should().BeTrue($"test project '{folder}' must exist");

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !IsBuildOutput(path)))
            {
                var source = File.ReadAllText(file);
                var relative = Path.GetRelativePath(root, file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(fileName) || fileName is "AssemblyInfo" or "GlobalUsings")
                    continue;

                var namespaceMatch = FileScopedNamespace.Match(source);
                if (!namespaceMatch.Success)
                    namespaceMatch = BlockNamespace.Match(source);

                if (!namespaceMatch.Success)
                {
                    failures.Add($"{folder}/{relative}: missing namespace declaration");
                    continue;
                }

                var directory = Path.GetDirectoryName(relative);
                var expectedNamespace = folder == "Tests/Fast"
                    ? "UniversalDeviceToolkit.Fast.Tests"
                    : "UniversalDeviceToolkit.Tests";
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    expectedNamespace += "." + directory.Replace(Path.DirectorySeparatorChar, '.');
                }

                var actualNamespace = namespaceMatch.Groups["name"].Value;
                if (!actualNamespace.Equals(expectedNamespace, StringComparison.Ordinal))
                {
                    failures.Add($"{folder}/{relative}: namespace '{actualNamespace}' != '{expectedNamespace}'");
                }

                var typeNames = TypeDeclaration.Matches(source)
                    .Select(match => match.Groups["name"].Value)
                    .ToHashSet(StringComparer.Ordinal);
                var normalizedRelative = relative.Replace(Path.DirectorySeparatorChar, '/');
                if (!typeNames.Contains(fileName)
                    && (!AggregateTypeContracts.TryGetValue(normalizedRelative, out var expectedTypes)
                        || expectedTypes.Any(typeName => !typeNames.Contains(typeName))))
                {
                    failures.Add($"{folder}/{relative}: no type named '{fileName}'");
                }
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TestFiles_ShouldNotUseTemporaryPhaseNames()
    {
        var repo = RepositoryPaths.FindRoot();
        var temporaryNames = TestProjectFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(repo, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Select(path => Path.GetFileName(path))
            .Where(name => name is not null && Regex.IsMatch(name, @"^Phase(?:[0-9A-Z]+)", RegexOptions.IgnoreCase))
            .ToArray();

        temporaryNames.Should().BeEmpty("tests should be named after the domain they verify");
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                         || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
}
