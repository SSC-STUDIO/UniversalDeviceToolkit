using System;
using System.Linq;
using System.Reflection;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Automation.Serialization;

internal static class AssemblyTypeLoaderExtensions
{
    /// <summary>
    /// Returns the loadable types from an assembly, tolerating <see cref="ReflectionTypeLoadException"/>
    /// (e.g. when a referenced dependency is missing). Any loader exceptions are logged once and the
    /// successfully loaded types are returned, so static-constructor type discovery never throws
    /// <see cref="TypeInitializationException"/>.
    /// </summary>
    internal static Type[] SafeGetTypes(this Assembly assembly)
    {
        if (assembly is null)
            return [];

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            try
            {
                Log.Instance.Warning($"Failed to load all types from assembly {assembly.GetName().Name}. Loader exceptions:");
                if (ex.LoaderExceptions is not null)
                {
                    foreach (var loaderEx in ex.LoaderExceptions)
                    {
                        if (loaderEx is not null)
                            Log.Instance.Warning($"  - {loaderEx.Message}", loaderEx);
                    }
                }
            }
            catch
            {
                // Logging itself must never let SafeGetTypes throw.
            }

            return ex.Types.Where(t => t is not null).OfType<Type>().ToArray();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Instance.Error($"Failed to get types from assembly {assembly.GetName().Name}", ex);
            }
            catch
            {
                // Swallow logging failures; SafeGetTypes must be exception-free.
            }

            return [];
        }
    }
}
