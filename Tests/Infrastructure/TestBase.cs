using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using FluentAssertions;

namespace UniversalDeviceToolkit.Tests;

/// <summary>
/// Test categories. After the Host test split, CI selects by project
/// (Contracts / Fast / Unit / Stateful). A class should carry at most one
/// Category trait: Security or Guard in Contracts, Unit elsewhere.
/// </summary>
public static class TestCategories
{
    public const string Unit = "Unit";

    /// <summary>Security-sensitive paths (injection, signatures, path traversal).</summary>
    public const string Security = "Security";

    /// <summary>Repository/architecture contract guards (CI YAML, payloads, layout).</summary>
    public const string Guard = "Guard";
}

/// <summary>
/// Base class for all unit tests with common setup and teardown
/// </summary>
public abstract class UnitTestBase : IDisposable
{
    protected UnitTestBase()
    {
        // Keep ordinary tests deterministic without changing process-wide defaults.
        // Tests that intentionally change localization state use the Localization collection.
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

        ForceKnownResourceCultures(culture);

        Setup();
    }

    /// <summary>
    /// Pins generated Resource.Culture on all known assemblies so English fallback
    /// strings are returned regardless of concurrent culture changes.
    /// Uses reflection so this infrastructure assembly has no compile-time dependency
    /// on any production module (WPF, Lib, Automation, Macro, Plugins).
    /// </summary>
    internal static void ForceKnownResourceCultures(System.Globalization.CultureInfo culture)
    {
        string[] resourceTypeNames =
        [
            "UniversalDeviceToolkit.Lib.Resources.Resource",
            "UniversalDeviceToolkit.Lib.Automation.Resources.Resource",
            "UniversalDeviceToolkit.Lib.Macro.Resources.Resource",
        ];

        foreach (var typeName in resourceTypeNames)
        {
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                    .FirstOrDefault(candidate => candidate is not null);
                if (type is null)
                    continue;
                var cultureProperty = type.GetProperty(
                    "Culture",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                cultureProperty?.SetValue(null, culture);
            }
            catch
            {
                // Not loaded / inaccessible — ignore.
            }
        }
    }

    protected virtual void Setup()
    {
    }

    public virtual void Dispose()
    {
        Cleanup();
    }

    protected virtual void Cleanup()
    {
    }
}

/// <summary>
/// Base class for tests that require temporary files
/// </summary>
public abstract class TemporaryFileTestBase : UnitTestBase
{
    protected readonly List<string> TempFiles = new();
    protected readonly List<string> TempDirectories = new();

    protected string CreateTempFile(string? content = null)
    {
        var tempPath = Path.GetTempFileName();
        TempFiles.Add(tempPath);
        
        if (content != null)
        {
            File.WriteAllText(tempPath, content);
        }
        
        return tempPath;
    }

    protected string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        TempDirectories.Add(tempDir);
        return tempDir;
    }

    protected override void Cleanup()
    {
        var cleanupFailures = new List<Exception>();

        foreach (var file in TempFiles.Where(File.Exists))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(new IOException($"Could not delete temporary file '{file}'.", ex));
            }
        }

        foreach (var dir in TempDirectories.Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                cleanupFailures.Add(new IOException($"Could not delete temporary directory '{dir}'.", ex));
            }
        }

        try
        {
            base.Cleanup();
        }
        catch (Exception ex)
        {
            cleanupFailures.Add(ex);
        }

        if (cleanupFailures.Count > 0)
        {
            throw new AggregateException("Temporary test resource cleanup failed.", cleanupFailures);
        }
    }
}

/// <summary>
/// Assertion helpers for common test scenarios
/// </summary>
public static class TestAssertions
{
    public static void ShouldBeSuccessful(this Action action)
    {
        action.Should().NotThrow();
    }

    public static void ShouldFailWith<TException>(this Action action) where TException : Exception
    {
        action.Should().Throw<TException>();
    }

    public static void ShouldContain<T>(this IEnumerable<T> collection, T expected)
    {
        collection.Should().Contain(expected);
    }

    public static void ShouldNotContain<T>(this IEnumerable<T> collection, T expected)
    {
        collection.Should().NotContain(expected);
    }

    public static void ShouldHaveProperty<T>(this T obj, string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property.Should().NotBeNull();
    }

    public static void ShouldHaveMethod<T>(this T obj, string methodName)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();
    }
}

/// <summary>
/// Test data generation utilities
/// </summary>
public static class TestDataGenerator
{
    private static int _counter = 0;

    public static string GenerateUniqueString(string prefix = "Test")
    {
        return $"{prefix}_{Guid.NewGuid():N}_{Interlocked.Increment(ref _counter)}";
    }

    public static Version GenerateVersion(int major = 1, int minor = 0, int build = 0, int revision = 0)
    {
        return new Version(major, minor, build, revision);
    }

    public static IEnumerable<T> CreateUniqueList<T>(int count, Func<int, T> factory)
    {
        return Enumerable.Range(0, count).Select(factory);
    }

    public static byte[] GenerateRandomBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    public static string GenerateRandomString(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}

/// <summary>
/// Async test helpers
/// </summary>
public static class AsyncTestHelpers
{
    public static async Task RetryAsync(
        Func<Task> action,
        int maxRetries = 3,
        int delayMs = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (maxRetries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (delayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMs));

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await action();
                return;
            }
            catch
            {
                if (i == maxRetries - 1)
                    throw;
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }
}
