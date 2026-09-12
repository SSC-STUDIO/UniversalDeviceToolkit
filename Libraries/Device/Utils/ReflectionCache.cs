using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Reflection cache helper to reduce reflection overhead.
/// </summary>
/// <remarks>
/// In high-frequency call scenarios, each reflection to get PropertyInfo incurs overhead.
/// This class optimizes performance by caching PropertyInfo.
/// </remarks>
public static class ReflectionCache
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _propertyByNameCache = new();

    /// <summary>
    /// Gets all public properties of a type, using cache to avoid repeated reflection.
    /// </summary>
    /// <param name="type">The type to get the property from.</param>
    /// <returns>Array of PropertyInfo.</returns>
    public static PropertyInfo[] GetCachedProperties(Type type)
    {
        return _propertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
    }

    /// <summary>
    /// Gets a property by name, using cache to avoid repeated reflection.
    /// </summary>
    /// <param name="type">The type to get the property from.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>PropertyInfo, or null if not found.</returns>
    public static PropertyInfo? GetCachedProperty(Type type, string propertyName)
    {
        var key = (type, propertyName);
        return _propertyByNameCache.GetOrAdd(key, k =>
        {
            var props = GetCachedProperties(k.Item1);
            foreach (var prop in props)
            {
                if (prop.Name == k.Item2)
                    return prop;
            }
            return null;
        });
    }

    /// <summary>
    /// Gets a property value using cached PropertyInfo.
    /// </summary>
    /// <param name="obj">The object to get the property value from.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>Property value, or null if retrieval fails.</returns>
    public static object? GetCachedPropertyValue(object obj, string propertyName)
    {
        if (obj == null) return null;
        
        var prop = GetCachedProperty(obj.GetType(), propertyName);
        return prop?.GetValue(obj);
    }

    /// <summary>
    /// Clears all caches.
    /// </summary>
    public static void ClearCache()
    {
        _propertyCache.Clear();
        _propertyByNameCache.Clear();
    }
}

/// <summary>
/// Cache helper for GPU power information.
/// </summary>
/// <remarks>
/// Caches nvidia-smi call results and failure state to avoid frequent external process calls.
/// </remarks>
public class GPUPowerInfoCache
{
    private int _cachedWattage = -1;
    private double _cachedVoltage;
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private bool _nvidiaSmiFailed;
    private DateTime _nvidiaSmiLastAttempt = DateTime.MinValue;
    
    private readonly TimeSpan _cacheDuration;
    private readonly TimeSpan _nvidiaSmiRetryInterval;

    /// <summary>
    /// Initializes a new GPUPowerInfoCache instance.
    /// </summary>
    /// <param name="cacheDuration">Cache duration, default 5 seconds.</param>
    /// <param name="nvidiaSmiRetryInterval">nvidia-smi retry interval, default 30 seconds.</param>
    public GPUPowerInfoCache(TimeSpan? cacheDuration = null, TimeSpan? nvidiaSmiRetryInterval = null)
    {
        _cacheDuration = cacheDuration ?? TimeSpan.FromSeconds(5);
        _nvidiaSmiRetryInterval = nvidiaSmiRetryInterval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Gets cached power information.
    /// </summary>
    /// <returns>Tuple of wattage and voltage.</returns>
    public (int wattage, double voltage) GetCached()
    {
        return (_cachedWattage, _cachedVoltage);
    }

    /// <summary>
    /// Updates the cache.
    /// </summary>
    /// <param name="wattage">Wattage in watts.</param>
    /// <param name="voltage">Voltage.</param>
    public void Update(int wattage, double voltage)
    {
        _cachedWattage = wattage;
        _cachedVoltage = voltage;
        _lastUpdateTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the cache is valid.
    /// </summary>
    /// <returns>Returns true if the cache is valid.</returns>
    public bool IsCacheValid()
    {
        return _cachedWattage >= 0 && (DateTime.UtcNow - _lastUpdateTime) < _cacheDuration;
    }

    /// <summary>
    /// Marks an nvidia-smi call as failed.
    /// </summary>
    public void MarkNvidiaSmiFailed()
    {
        _nvidiaSmiFailed = true;
        _nvidiaSmiLastAttempt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks whether an nvidia-smi call should be attempted.
    /// </summary>
    /// <returns>Returns true if a call should be attempted.</returns>
    public bool ShouldTryNvidiaSmi()
    {
        if (!_nvidiaSmiFailed) return true;
        return (DateTime.UtcNow - _nvidiaSmiLastAttempt) > _nvidiaSmiRetryInterval;
    }

    /// <summary>
    /// Resets the nvidia-smi failure state.
    /// </summary>
    public void ResetNvidiaSmiFailed()
    {
        _nvidiaSmiFailed = false;
    }
}
