using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class CiWorkflowGuardTests
{
    [Fact]
    public void CiTestsWorkflow_ShouldRunContractsFailFast()
    {
        var workflow = ReadWorkflow("Ci-tests.yml");
        var step = workflow.Job("build-test-and-smoke").Step("Test (Contracts - fail fast)");

        step.Run.Should().Contain("UniversalDeviceToolkit.Tests.Contracts/UniversalDeviceToolkit.Tests.Contracts.csproj");
        step.Run.Should().NotContain("--filter");
        step.OptionValue("--logger").Should().Be("trx;LogFileName=UniversalDeviceToolkit.Tests.Contracts.trx");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldRunFastTests()
    {
        var step = ReadWorkflow("Ci-tests.yml").Job("build-test-and-smoke").Step("Test (Fast unit tests)");

        step.Run.Should().Contain("UniversalDeviceToolkit.Fast.Tests/UniversalDeviceToolkit.Fast.Tests.csproj");
        step.OptionValue("--logger").Should().Be("trx;LogFileName=UniversalDeviceToolkit.Fast.Tests.trx");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldRunUnitSuite()
    {
        var step = ReadWorkflow("Ci-tests.yml").Job("build-test-and-smoke").Step("Test (Unit)");

        step.Run.Should().Contain("UniversalDeviceToolkit.Tests/UniversalDeviceToolkit.Tests.csproj");
        step.Run.Should().NotContain("--filter");
        step.OptionValue("--logger").Should().Be("trx;LogFileName=UniversalDeviceToolkit.Tests.trx");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldRunStatefulSuiteOnce()
    {
        var step = ReadWorkflow("Ci-tests.yml").Job("build-test-and-smoke").Step("Test (Stateful)");

        step.Run.Should().Contain("UniversalDeviceToolkit.Tests.Stateful/UniversalDeviceToolkit.Tests.Stateful.csproj");
        step.Run.Should().NotContain("--filter");
        step.OptionValue("--logger").Should().Be("trx;LogFileName=UniversalDeviceToolkit.Tests.Stateful.trx");
    }

    [Fact]
    public void TestRunner_ShouldParallelizeTestsOutsideExplicitStateCollections()
    {
        var unitRunner = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Tests", "xunit.runner.json");
        var contractsRunner = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Tests.Contracts", "xunit.runner.json");
        var statefulRunner = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Tests.Stateful", "xunit.runner.json");
        var collections = RepositoryPaths.ReadFile(
            "UniversalDeviceToolkit.Tests.Stateful", "Settings", "SettingsTestCollection.cs");
        var unitCollections = RepositoryPaths.ReadFile(
            "UniversalDeviceToolkit.Tests", "Settings", "SettingsTestCollection.cs");

        unitRunner.Should().Contain("\"parallelizeAssembly\": true");
        unitRunner.Should().Contain("\"parallelizeTestCollections\": true");
        unitRunner.Should().Contain("\"maxParallelThreads\": 0");
        contractsRunner.Should().Contain("\"parallelizeTestCollections\": true");
        statefulRunner.Should().Contain("\"parallelizeTestCollections\": false");
        collections.Should().Contain("DisableParallelization = true");
        collections.Should().Contain("Localization");
        collections.Should().Contain("Settings");
        collections.Should().Contain("ProcessState");
        unitCollections.Should().Contain("DisableParallelization = true");
        unitCollections.Should().Contain("ProcessState");
    }

    [Fact]
    public void FailFastScript_ShouldRunContractsAndFastLayers()
    {
        var script = RepositoryPaths.ReadFile("Scripts", "Run-TestFailFast.ps1");

        script.Should().Contain("UniversalDeviceToolkit.Tests.Contracts/UniversalDeviceToolkit.Tests.Contracts.csproj");
        script.Should().Contain("UniversalDeviceToolkit.Fast.Tests/UniversalDeviceToolkit.Fast.Tests.csproj");
        script.Should().NotContain("Category=Security|Category=Guard");
        script.Should().NotContain("Category=Plugin");
        script.Should().NotContain("Category=Unit&Category!=Coverage");
        script.Should().NotContain("Category=Smoke");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldNotRepeatPluginUnitAndSmokeLayers()
    {
        var stepNames = ReadWorkflow("Ci-tests.yml")
            .Job("build-test-and-smoke")
            .Steps
            .Select(step => step.Name)
            .Where(name => name is not null)
            .Cast<string>();

        stepNames.Should().NotContain(name => name.StartsWith("Test (Plugin category", StringComparison.Ordinal));
        stepNames.Should().NotContain(name => name.StartsWith("Test (Unit category", StringComparison.Ordinal));
        stepNames.Should().NotContain(name => name.StartsWith("Test (Main app plugin UI smoke contract", StringComparison.Ordinal));
        AllStepText(ReadWorkflow("Ci-tests.yml")).Should().NotContain("UniversalDeviceToolkit.Tests.Smoke.trx");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldNotRunWpfL10nCoverageScript()
    {
        var job = ReadWorkflow("Ci-tests.yml").Job("build-test-and-smoke");

        job.Steps.Should().NotContain(
            s => s.Name != null && s.Name.Contains("WPF l10n", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CiTestsWorkflow_ShouldRunWindowsTestPreflight()
    {
        var workflow = ReadWorkflow("Ci-tests.yml");
        var script = RepositoryPaths.ReadFile("Scripts", "Test-WindowsTestEnvironment.ps1");
        var step = workflow.Job("build-test-and-smoke").Step("Verify Windows test prerequisites");

        step.Run.Should().Contain("Test-WindowsTestEnvironment.ps1");
        script.Should().Contain("UniversalDeviceToolkit.sln");
        script.Should().Contain("--list-sdks");
        script.Should().Contain("HKCU:\\Software\\UniversalDeviceToolkit");
        script.Should().Contain("cleanupFailures");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldPublishTestResultsAndDiagnosticsAsArtifacts()
    {
        var step = ReadWorkflow("Ci-tests.yml").Job("build-test-and-smoke")
            .Step("Upload Windows test results and diagnostics");
        var paths = step.WithValue("path");

        step.Uses.Should().Be("actions/upload-artifact@v7");
        step.Condition.Should().Be("always()");
        step.WithValue("name").Should().Be("windows-test-results-${{ github.run_id }}");
        paths.Should().Contain("**/UniversalDeviceToolkit.Tests.Contracts.trx");
        paths.Should().Contain("**/UniversalDeviceToolkit.Fast.Tests.trx");
        paths.Should().Contain("**/UniversalDeviceToolkit.Tests.trx");
        paths.Should().Contain("**/UniversalDeviceToolkit.Tests.Stateful.trx");
        paths.Should().Contain("**/TestResults/**/*.dmp");
        paths.Should().Contain("**/TestResults/**/*.log");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldPublishTestResultsAndOpenCoverCoverage()
    {
        var text = AllStepText(ReadWorkflow("Ci-tests.yml"));

        text.Should().Contain(".trx");
        text.Should().Contain("coverage.opencover.xml");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldPublishAndVerifyPortableCliOnEveryRuntime()
    {
        var workflow = ReadWorkflow("Ci-tests.yml");
        var text = AllStepText(workflow.Job("cross-platform-cli").Steps);
        var rawWorkflow = RepositoryPaths.ReadFile(".github", "workflows", "Ci-tests.yml");

        // The portable CLI has no path filter: any change can break it, so it rides the main CI trigger.
        workflow.Triggers["pull_request"].Paths.Should().BeEmpty();
        workflow.Triggers.Should().ContainKey("workflow_dispatch");
        foreach (var runtime in new[] { "win-x64", "linux-x64", "osx-arm64", "osx-x64" })
            rawWorkflow.Should().Contain($"runtime: {runtime}");

        text.Should().Contain("dotnet publish $project --configuration Release --self-contained false");
        text.Should().Contain("'udt.dll', 'udt.deps.json', 'udt.runtimeconfig.json', 'resources/device-packs.json'");
        text.Should().Contain("Published CLI returned unexpected product name");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldRunCrossPlatformTestsOnLinuxForPullRequests()
    {
        var workflow = ReadWorkflow("Ci-tests.yml");
        var job = workflow.Job("cross-platform-cli");
        var text = AllStepText(job.Steps);
        var rawWorkflow = RepositoryPaths.ReadFile(".github", "workflows", "Ci-tests.yml");

        workflow.Triggers.Should().ContainKey("pull_request");
        job.RunsOn.Should().Be("${{ matrix.os }}");
        rawWorkflow.Should().Contain("ubuntu-latest");
        text.Should().Contain("UniversalDeviceToolkit.CrossPlatform.Tests");
        text.Should().Contain("UniversalDeviceToolkit.Lib.Shared");
        text.Should().Contain("dotnet test");
        text.Should().Contain("Smoke diagnostics CLI");
        text.Should().Contain("-- status");
        text.Should().Contain("-- doctor");
        text.Should().Contain("-- json");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldCollectCoverageAndPublishArtifacts()
    {
        var text = AllStepText(ReadWorkflow("Ci-tests.yml"));

        text.Should().Contain("--collect:\"XPlat Code Coverage\"");
        text.Should().Contain("UniversalDeviceToolkit.Tests/coverlet.runsettings");
        text.Should().Contain("coverage.cobertura.xml");
    }

    [Fact]
    public void WslVerificationScript_ShouldRunLockedLinuxBuildAndSmokeChecks()
    {
        var script = RepositoryPaths.ReadFile("Scripts", "Test-CrossPlatformInWsl.ps1");

        script.Should().Contain("never installs a distribution automatically");
        script.Should().NotContain("$wslCommand.Source --install");
        script.Should().Contain("--locked-mode");
        script.Should().Contain("UniversalDeviceToolkit.Platform.Linux");
        script.Should().Contain("UniversalDeviceToolkit.CrossPlatform.Tests");
        script.Should().Contain("cross-platform diagnostics");
        script.Should().Contain("Hardware identity");
        script.Should().Contain("Universal Device Toolkit");
        script.Should().Contain("No WSL distribution is installed");
    }

    [Fact]
    public void CiTestsWorkflow_ShouldLintTypecheckAndTestElectronUi()
    {
        var job = ReadWorkflow("Ci-tests.yml").Job("electron-ui-tests");
        var unicode = job.Step("Check source Unicode");
        var lint = job.Step("Lint Electron UI");
        var typecheck = job.Step("Typecheck Electron UI");
        var test = job.Step("Test Electron UI");

        unicode.Run.Should().Contain("Tools/CheckSourceUnicode/check-unicode.mjs");
        lint.Run.Should().Contain("npm run lint");
        typecheck.Run.Should().Contain("npm run typecheck");
        test.Run.Should().Contain("npm test");
    }

    private static GitHubWorkflowContract ReadWorkflow(string fileName) =>
        GitHubWorkflowContract.Parse(RepositoryPaths.ReadFile(".github", "workflows", fileName));

    private static string AllStepText(GitHubWorkflowContract workflow) => AllStepText(workflow.Steps);

    private static string AllStepText(IEnumerable<WorkflowStepContract> steps)
    {
        var values = new List<string>();
        foreach (var step in steps)
        {
            if (step.Name is not null)
                values.Add(step.Name);
            if (step.Uses is not null)
                values.Add(step.Uses);
            if (step.Run is not null)
                values.Add(step.Run);
            values.AddRange(step.With.Values);
        }

        return string.Join(Environment.NewLine, values);
    }
}
