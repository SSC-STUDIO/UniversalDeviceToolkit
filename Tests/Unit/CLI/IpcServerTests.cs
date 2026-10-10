using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Automation.CLI;
using UniversalDeviceToolkit.Lib.Automation.CLI.Features;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Trait("Category", TestCategories.Unit)]
public class IpcServerTests
{
    [Fact]
    public void CreatePipeSecurity_ShouldOnlyAllowAdministrators()
    {
        var method = typeof(IpcServer).GetMethod("CreatePipeSecurity", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var security = method!.Invoke(null, []) as PipeSecurity;
        security.Should().NotBeNull();

        var rules = security!
            .GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        var adminRules = rules.Where(IsAdministratorReadWriteAllowRule);
        var currentUserRules = rules.Where(IsCurrentUserReadWriteAllowRule);

        adminRules.Should().ContainSingle();
        currentUserRules.Should().ContainSingle();
        rules.All(rule => IsAdministratorReadWriteAllowRule(rule) || IsCurrentUserReadWriteAllowRule(rule)).Should().BeTrue();
        rules.Should().NotContain(rule =>
            rule.AccessControlType == AccessControlType.Deny &&
            rule.IdentityReference is SecurityIdentifier &&
            ((SecurityIdentifier)rule.IdentityReference).IsWellKnown(WellKnownSidType.WorldSid));
    }

    [Fact]
    public void CreatePipeSecurity_ShouldGrantReadWriteAccess()
    {
        var method = typeof(IpcServer).GetMethod("CreatePipeSecurity", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var security = method!.Invoke(null, []) as PipeSecurity;
        security.Should().NotBeNull();

        var rules = security!
            .GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        var adminRule = rules.FirstOrDefault(IsAdministratorReadWriteAllowRule);
        adminRule.Should().NotBeNull();
        adminRule!.PipeAccessRights.Should().HaveFlag(PipeAccessRights.ReadWrite);
    }

    [Fact]
    public void CreatePipeSecurity_ShouldNotAllowWorldSid()
    {
        var method = typeof(IpcServer).GetMethod("CreatePipeSecurity", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var security = method!.Invoke(null, []) as PipeSecurity;
        security.Should().NotBeNull();

        var rules = security!
            .GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        rules.Should().NotContain(rule =>
            rule.IdentityReference is SecurityIdentifier &&
            ((SecurityIdentifier)rule.IdentityReference).IsWellKnown(WellKnownSidType.WorldSid));
    }

    [Fact]
    public void CreatePipeSecurity_ShouldNotAllowAnonymousSid()
    {
        var method = typeof(IpcServer).GetMethod("CreatePipeSecurity", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var security = method!.Invoke(null, []) as PipeSecurity;
        security.Should().NotBeNull();

        var rules = security!
            .GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        rules.Should().NotContain(rule =>
            rule.IdentityReference is SecurityIdentifier &&
            ((SecurityIdentifier)rule.IdentityReference).IsWellKnown(WellKnownSidType.AnonymousSid));
    }

    [Fact]
    public void CreatePipeServerStream_ShouldCreateValidPipe()
    {
        var method = typeof(IpcServer).GetMethod("CreatePipeServerStream", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void PeerElevation_ShouldBeRequiredOnlyWhenServerIsElevated(
        bool serverElevated,
        bool peerElevated,
        bool expectedAllowed)
    {
        var method = typeof(IpcServer).GetMethod("IsPeerElevationAllowed", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var result = method!.Invoke(null, [serverElevated, peerElevated]);

        result.Should().Be(expectedAllowed);
    }

    [Fact]
    public async Task ListFeaturesAsync_WhenCacheExists_ShouldReturnCachedFeatureList()
    {
        var method = typeof(IpcServer).GetMethod("ListFeaturesAsync", BindingFlags.NonPublic | BindingFlags.Static);
        var cacheField = typeof(IpcServer).GetField("_supportedFeaturesCache", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        cacheField.Should().NotBeNull();

        var original = cacheField!.GetValue(null);
        cacheField.SetValue(null, "power-mode\nbattery");
        try
        {
            var resultTask = method!.Invoke(null, [CancellationToken.None]) as Task<string?>;
            resultTask.Should().NotBeNull();

            var result = await resultTask!;

            result.Should().Be("power-mode\nbattery");
        }
        finally
        {
            cacheField.SetValue(null, original);
        }
    }

    [Fact]
    public async Task BuildSupportedFeatureListAsync_WhenFeatureProbeFails_ShouldSkipOnlyFailedFeature()
    {
        var registrations = new IFeatureRegistration[]
        {
            new TestFeatureRegistration("power-mode", true),
            new TestFeatureRegistration("battery", false),
            new TestFeatureRegistration("refresh-rate", true),
            new TestFeatureRegistration("broken", throwOnSupportCheck: true),
        };

        var method = typeof(IpcServer).GetMethod("BuildSupportedFeatureListAsync", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var result = await InvokeBuildSupportedFeatureListAsync(method!, registrations);

        GetFeatureList(result).Should().Be("power-mode\nrefresh-rate");
        GetHasProbeFailures(result).Should().BeTrue();
    }

    [Fact]
    public async Task BuildSupportedFeatureListAsync_WhenFeatureProbesSucceed_ShouldReturnCacheableFeatureList()
    {
        var registrations = new IFeatureRegistration[]
        {
            new TestFeatureRegistration("power-mode", true),
            new TestFeatureRegistration("battery", false),
            new TestFeatureRegistration("refresh-rate", true),
        };

        var method = typeof(IpcServer).GetMethod("BuildSupportedFeatureListAsync", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var result = await InvokeBuildSupportedFeatureListAsync(method!, registrations);

        GetFeatureList(result).Should().Be("power-mode\nrefresh-rate");
        GetHasProbeFailures(result).Should().BeFalse();
    }

    private static async Task<object> InvokeBuildSupportedFeatureListAsync(MethodInfo method, IFeatureRegistration[] registrations)
    {
        var task = method.Invoke(null, [registrations]) as Task;
        task.Should().NotBeNull();

        await task!;

        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        result.Should().NotBeNull();
        return result!;
    }

    private static string GetFeatureList(object result)
        => result.GetType().GetProperty("FeatureList")?.GetValue(result) as string
           ?? throw new InvalidOperationException("Missing FeatureList result property.");

    private static bool GetHasProbeFailures(object result)
        => result.GetType().GetProperty("HasProbeFailures")?.GetValue(result) as bool?
           ?? throw new InvalidOperationException("Missing HasProbeFailures result property.");

    private static bool IsAdministratorReadWriteAllowRule(PipeAccessRule rule)
    {
        return rule.AccessControlType == AccessControlType.Allow &&
               rule.IdentityReference is SecurityIdentifier sid &&
               sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) &&
               rule.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite);
    }

    private static bool IsCurrentUserReadWriteAllowRule(PipeAccessRule rule)
    {
        return rule.AccessControlType == AccessControlType.Allow &&
               rule.IdentityReference is SecurityIdentifier sid &&
               WindowsIdentity.GetCurrent().User is { } currentUser &&
               sid.Equals(currentUser) &&
               rule.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite);
    }

    private sealed class TestFeatureRegistration(string name, bool supported = false, bool throwOnSupportCheck = false) : IFeatureRegistration
    {
        public string Name { get; } = name;

        public Task<bool> IsSupportedAsync()
        {
            if (throwOnSupportCheck)
                throw new InvalidOperationException("Probe failed");

            return Task.FromResult(supported);
        }

        public Task<IEnumerable<string>> GetValuesAsync() => throw new NotSupportedException();

        public Task<string> GetValueAsync() => throw new NotSupportedException();

        public Task SetValueAsync(string value) => throw new NotSupportedException();
    }
}
