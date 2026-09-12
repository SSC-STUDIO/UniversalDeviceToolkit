using FluentAssertions;
using UniversalDeviceToolkit.Tests;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class ElevationLauncherTests
{
    [Fact]
    public void Launch_WhenNoCommandIsProvided_ShouldReturnUsage()
    {
        var result = new ElevationLauncher().Launch([]);

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("Usage: udt elevate");
    }

    [Fact]
    public void Launch_WhenNotWindows_ShouldExplainPlatformElevation()
    {
        if (OperatingSystem.IsWindows())
            return;

        var result = new ElevationLauncher().Launch(["set", "cpu-governor", "performance"]);

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("UAC");
        result.Detail.Should().Contain("sudo");
        result.Detail.Should().Contain("polkit");
    }

    [Fact]
    public void Launch_WhenArgumentContainsUnsafeCharacters_ShouldRejectWithoutStartingProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = new ElevationLauncher(CreateEmptySnapshot()).Launch(["set", "cpu-governor", "performance&whoami"]);

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("unsafe characters");
    }

    [Fact]
    public void Launch_WhenNoTrustedHostCanBeResolved_ShouldFailClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = new ElevationLauncher(CreateEmptySnapshot()).Launch(["set", "cpu-governor", "performance"]);

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().ContainAny("trusted", "absolute", "could not be resolved", "does not exist", "empty");
    }

    [Fact]
    public void TryBuildWindowsLaunchPlan_WhenAppHostExists_ShouldUseAbsoluteAppHostAndSafeWorkingDirectory()
    {
        using var workspace = new ElevationTestWorkspace();
        var appHost = workspace.CreateFile("udt.exe");
        var systemDirectory = workspace.CreateDirectory("system32");
        var currentDirectory = workspace.CreateDirectory("cwd");
        workspace.CreateFile(Path.Combine(currentDirectory, "dotnet.exe"));

        var environment = workspace.CreateSnapshot(
            processPath: appHost,
            entryAssemblyLocation: workspace.CreateFile("udt.dll"),
            currentDirectory: currentDirectory,
            systemDirectory: systemDirectory);

        var built = ElevationLauncher.TryBuildWindowsLaunchPlan(
            ["set", "cpu-governor", "performance"],
            environment,
            out var plan,
            out var error);

        built.Should().BeTrue(error);
        plan.FileName.Should().Be(Path.GetFullPath(appHost));
        Path.IsPathRooted(plan.FileName).Should().BeTrue();
        plan.FileName.Should().NotBe("dotnet");
        Path.GetFileName(plan.FileName).Should().Be("udt.exe");
        plan.Arguments.Should().Equal("set", "cpu-governor", "performance");
        plan.WorkingDirectory.Should().Be(Path.GetFullPath(systemDirectory));
        plan.WorkingDirectory.Should().NotBe(Path.GetFullPath(currentDirectory));
    }

    [Fact]
    public void TryBuildWindowsLaunchPlan_WhenFrameworkDependent_ShouldResolveAbsoluteTrustedDotNet()
    {
        using var workspace = new ElevationTestWorkspace();
        var trustedRoot = workspace.CreateDirectory("Program Files");
        var trustedDotNet = workspace.CreateFile(Path.Combine(trustedRoot, "dotnet", "dotnet.exe"));
        var entryDll = workspace.CreateFile("udt.dll");
        var currentDirectory = workspace.CreateDirectory("cwd");
        var plantedHost = workspace.CreateFile(Path.Combine(currentDirectory, "dotnet.exe"));
        var systemDirectory = workspace.CreateDirectory("system32");

        var environment = workspace.CreateSnapshot(
            processPath: trustedDotNet,
            entryAssemblyLocation: entryDll,
            currentDirectory: currentDirectory,
            systemDirectory: systemDirectory,
            dotNetHostPath: plantedHost,
            programFiles: trustedRoot);

        var built = ElevationLauncher.TryBuildWindowsLaunchPlan(
            ["set", "cpu-governor", "performance"],
            environment,
            out var plan,
            out var error);

        built.Should().BeTrue(error);
        plan.FileName.Should().Be(Path.GetFullPath(trustedDotNet));
        Path.IsPathRooted(plan.FileName).Should().BeTrue();
        plan.FileName.Should().NotBe("dotnet");
        plan.FileName.Should().NotBe(Path.GetFullPath(plantedHost));
        plan.Arguments.Should().Equal(Path.GetFullPath(entryDll), "set", "cpu-governor", "performance");
        plan.WorkingDirectory.Should().Be(Path.GetFullPath(systemDirectory));
    }

    [Fact]
    public void TryBuildWindowsLaunchPlan_WhenOnlyCurrentDirectoryHostExists_ShouldRejectHijack()
    {
        using var workspace = new ElevationTestWorkspace();
        var currentDirectory = workspace.CreateDirectory("cwd");
        var plantedHost = workspace.CreateFile(Path.Combine(currentDirectory, "dotnet.exe"));
        var entryDll = workspace.CreateFile("udt.dll");

        var environment = workspace.CreateSnapshot(
            processPath: plantedHost,
            entryAssemblyLocation: entryDll,
            currentDirectory: currentDirectory,
            systemDirectory: workspace.CreateDirectory("system32"),
            dotNetHostPath: plantedHost);

        var built = ElevationLauncher.TryBuildWindowsLaunchPlan(
            ["set", "cpu-governor", "performance"],
            environment,
            out _,
            out var error);

        built.Should().BeFalse();
        error.Should().Contain("current directory");
    }

    [Fact]
    public void TryVerifyTrustedExecutable_WhenBareDotNetName_ShouldReject()
    {
        var verified = ElevationLauncher.TryVerifyTrustedExecutable(
            "dotnet",
            expectedFileName: null,
            currentDirectory: Path.GetTempPath(),
            rejectCurrentDirectory: true,
            fileExists: static _ => true,
            out _,
            out var error);

        verified.Should().BeFalse();
        error.Should().Contain("absolute");
    }

    [Fact]
    public void TryVerifyTrustedExecutable_WhenRelativeDotNet_ShouldReject()
    {
        var verified = ElevationLauncher.TryVerifyTrustedExecutable(
            Path.Combine(".", "dotnet.exe"),
            expectedFileName: null,
            currentDirectory: Path.GetTempPath(),
            rejectCurrentDirectory: true,
            fileExists: static _ => true,
            out _,
            out var error);

        verified.Should().BeFalse();
        error.Should().Contain("absolute");
    }

    [Fact]
    public void TryVerifyTrustedExecutable_WhenPlantedInCurrentDirectory_ShouldReject()
    {
        using var workspace = new ElevationTestWorkspace();
        var currentDirectory = workspace.CreateDirectory("cwd");
        var plantedHost = workspace.CreateFile(Path.Combine(currentDirectory, "dotnet.exe"));

        var verified = ElevationLauncher.TryVerifyTrustedExecutable(
            plantedHost,
            expectedFileName: null,
            currentDirectory: currentDirectory,
            rejectCurrentDirectory: true,
            fileExists: File.Exists,
            out _,
            out var error);

        verified.Should().BeFalse();
        error.Should().Contain("current directory");
    }

    [Fact]
    public void TryVerifyTrustedExecutable_WhenAbsoluteTrustedHostExists_ShouldAccept()
    {
        using var workspace = new ElevationTestWorkspace();
        var trustedHost = workspace.CreateFile(Path.Combine("Program Files", "dotnet", "dotnet.exe"));
        var currentDirectory = workspace.CreateDirectory("cwd");

        var verified = ElevationLauncher.TryVerifyTrustedExecutable(
            trustedHost,
            expectedFileName: null,
            currentDirectory: currentDirectory,
            rejectCurrentDirectory: true,
            fileExists: File.Exists,
            out var verifiedPath,
            out var error);

        verified.Should().BeTrue(error);
        verifiedPath.Should().Be(Path.GetFullPath(trustedHost));
        Path.IsPathRooted(verifiedPath).Should().BeTrue();
    }

    [Fact]
    public void TryVerifyTrustedExecutable_WhenMissingFile_ShouldReject()
    {
        using var workspace = new ElevationTestWorkspace();
        var missing = Path.Combine(workspace.Root, "Program Files", "dotnet", "dotnet.exe");

        var verified = ElevationLauncher.TryVerifyTrustedExecutable(
            missing,
            expectedFileName: null,
            currentDirectory: workspace.CreateDirectory("cwd"),
            rejectCurrentDirectory: true,
            fileExists: File.Exists,
            out _,
            out var error);

        verified.Should().BeFalse();
        error.Should().Contain("does not exist");
    }

    [Fact]
    public void TryResolveTrustedDotNetHost_ShouldIgnoreCurrentDirectoryPlantAndUseProgramFiles()
    {
        using var workspace = new ElevationTestWorkspace();
        var currentDirectory = workspace.CreateDirectory("cwd");
        workspace.CreateFile(Path.Combine(currentDirectory, "dotnet.exe"));
        var trustedRoot = workspace.CreateDirectory("Program Files");
        var trustedHost = workspace.CreateFile(Path.Combine(trustedRoot, "dotnet", "dotnet.exe"));

        var environment = workspace.CreateSnapshot(
            currentDirectory: currentDirectory,
            systemDirectory: workspace.CreateDirectory("system32"),
            dotNetHostPath: Path.Combine(currentDirectory, "dotnet.exe"),
            programFiles: trustedRoot);

        var resolved = ElevationLauncher.TryResolveTrustedDotNetHost(environment, out var verifiedPath, out var error);

        resolved.Should().BeTrue(error);
        verifiedPath.Should().Be(Path.GetFullPath(trustedHost));
    }

    [Fact]
    public void TryResolveTrustedDotNetHost_WhenDotNetRootIsCurrentDirectory_ShouldReject()
    {
        using var workspace = new ElevationTestWorkspace();
        var currentDirectory = workspace.CreateDirectory("cwd");
        workspace.CreateFile(Path.Combine(currentDirectory, "dotnet.exe"));

        var environment = workspace.CreateSnapshot(
            currentDirectory: currentDirectory,
            systemDirectory: workspace.CreateDirectory("system32"),
            dotNetRoot: currentDirectory);

        var resolved = ElevationLauncher.TryResolveTrustedDotNetHost(environment, out _, out var error);

        resolved.Should().BeFalse();
        error.Should().Contain("current directory");
    }

    [Fact]
    public void ResolveSafeWorkingDirectory_ShouldPreferSystemDirectoryOverCurrentDirectory()
    {
        using var workspace = new ElevationTestWorkspace();
        var systemDirectory = workspace.CreateDirectory("system32");
        var currentDirectory = workspace.CreateDirectory("cwd");
        var hostDirectory = workspace.CreateDirectory(Path.Combine("Program Files", "dotnet"));

        var environment = workspace.CreateSnapshot(
            currentDirectory: currentDirectory,
            systemDirectory: systemDirectory);

        var workingDirectory = ElevationLauncher.ResolveSafeWorkingDirectory(environment, hostDirectory);

        workingDirectory.Should().Be(Path.GetFullPath(systemDirectory));
        workingDirectory.Should().NotBe(Path.GetFullPath(currentDirectory));
    }

    [Fact]
    public void ElevationLauncherSource_ShouldNotLaunchBareDotNetFromCurrentDirectory()
    {
        var source = RepositoryPaths.ReadFile(
            "Apps/CrossPlatformCLI",
            "ElevationLauncher.cs");

        source.Should().NotContain("return (\"dotnet\"");
        source.Should().NotContain("WorkingDirectory = Environment.CurrentDirectory");
        source.Should().NotContain("FileName = \"dotnet\"");
        source.Should().NotContain("new ProcessStartInfo(\"dotnet\"");
        source.Should().Contain("TryResolveTrustedDotNetHost");
        source.Should().Contain("ResolveSafeWorkingDirectory");
        source.Should().Contain("rejectCurrentDirectory");
    }

    private static ElevationEnvironmentSnapshot CreateEmptySnapshot()
    {
        return new ElevationEnvironmentSnapshot(
            ProcessPath: null,
            EntryAssemblyLocation: null,
            CurrentDirectory: Path.GetTempPath(),
            SystemDirectory: Path.GetTempPath(),
            DotNetHostPath: null,
            DotNetRoot: null,
            ProgramFiles: null,
            ProgramFilesX86: null,
            FileExists: static _ => false,
            DirectoryExists: Directory.Exists);
    }

    private sealed class ElevationTestWorkspace : IDisposable
    {
        public ElevationTestWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "udt-elevation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativeOrAbsolutePath)
        {
            var path = Path.IsPathRooted(relativeOrAbsolutePath)
                ? relativeOrAbsolutePath
                : Path.Combine(Root, relativeOrAbsolutePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, [0x4D, 0x5A]);
            return path;
        }

        public ElevationEnvironmentSnapshot CreateSnapshot(
            string? processPath = null,
            string? entryAssemblyLocation = null,
            string? currentDirectory = null,
            string? systemDirectory = null,
            string? dotNetHostPath = null,
            string? dotNetRoot = null,
            string? programFiles = null,
            string? programFilesX86 = null)
        {
            return new ElevationEnvironmentSnapshot(
                ProcessPath: processPath,
                EntryAssemblyLocation: entryAssemblyLocation,
                CurrentDirectory: currentDirectory ?? Root,
                SystemDirectory: systemDirectory ?? Root,
                DotNetHostPath: dotNetHostPath,
                DotNetRoot: dotNetRoot,
                ProgramFiles: programFiles,
                ProgramFilesX86: programFilesX86,
                FileExists: File.Exists,
                DirectoryExists: Directory.Exists);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
