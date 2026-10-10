using System.Reflection;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features;

/// <summary>
/// Serializes tests that mutate the Compatibility static cache via reflection
/// so they cannot race or leak state into parallel test classes.
/// </summary>
[CollectionDefinition("PowerModeFeatureTests", DisableParallelization = true)]
public class PowerModeFeatureTestCollection
{
}

/// <summary>
/// Provides deterministic reset of the static Compatibility cache before and
/// after every test that mutates it. Use as <see cref="IClassFixture{TFixture}"/>
/// so xUnit creates a fresh fixture per test class instance.
/// </summary>
public sealed class CompatibilityCacheFixture : IDisposable
{
    public CompatibilityCacheFixture() => Reset();

    public void Dispose() => Reset();

    public static void Reset()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        
        var lazyField = typeof(Compatibility).GetField("_machineInformationLazy", BindingFlags.NonPublic | BindingFlags.Static);
        if (lazyField != null)
        {
            var method = typeof(Compatibility).GetMethod("GetMachineInformationInternalAsync", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                var del = Delegate.CreateDelegate(typeof(Func<Task<MachineInformation>>), method);
                var newLazy = Activator.CreateInstance(typeof(Lazy<Task<MachineInformation>>), [del, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication]);
                lazyField.SetValue(null, newLazy);
            }
        }

        typeof(Compatibility)
            .GetField("_isCompatible", BindingFlags.NonPublic | BindingFlags.Static)
            !.SetValue(null, null);
    }
}