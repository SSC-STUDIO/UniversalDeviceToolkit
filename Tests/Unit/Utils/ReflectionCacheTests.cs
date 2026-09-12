using System;
using System.Reflection;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ReflectionCacheTests : UnitTestBase
{
    private class TestClass
    {
        public int PublicProperty { get; set; } = 42;
        public string StringProperty { get; set; } = "test";
    }

    [Fact]
    public void GetCachedProperties_ShouldReturnPublicProperties()
    {
        var properties = ReflectionCache.GetCachedProperties(typeof(TestClass));

        properties.Should().NotBeEmpty();
        properties.Should().Contain(p => p.Name == nameof(TestClass.PublicProperty));
        properties.Should().Contain(p => p.Name == nameof(TestClass.StringProperty));
    }

    [Fact]
    public void GetCachedProperties_WhenCalledMultipleTimes_ShouldReturnSameInstance()
    {
        var properties1 = ReflectionCache.GetCachedProperties(typeof(TestClass));
        var properties2 = ReflectionCache.GetCachedProperties(typeof(TestClass));

        properties1.Should().BeSameAs(properties2);
    }

    [Fact]
    public void GetCachedProperty_ShouldReturnCorrectProperty()
    {
        var property = ReflectionCache.GetCachedProperty(typeof(TestClass), nameof(TestClass.PublicProperty));

        property.Should().NotBeNull();
        property!.Name.Should().Be(nameof(TestClass.PublicProperty));
    }

    [Fact]
    public void GetCachedProperty_WhenPropertyDoesNotExist_ShouldReturnNull()
    {
        var property = ReflectionCache.GetCachedProperty(typeof(TestClass), "NonExistentProperty");

        property.Should().BeNull();
    }

    [Fact]
    public void GetCachedProperty_WhenCalledMultipleTimes_ShouldReturnSameInstance()
    {
        var property1 = ReflectionCache.GetCachedProperty(typeof(TestClass), nameof(TestClass.PublicProperty));
        var property2 = ReflectionCache.GetCachedProperty(typeof(TestClass), nameof(TestClass.PublicProperty));

        property1.Should().BeSameAs(property2);
    }

    [Fact]
    public void GetCachedPropertyValue_ShouldReturnCorrectValue()
    {
        var obj = new TestClass { PublicProperty = 100 };

        var value = ReflectionCache.GetCachedPropertyValue(obj, nameof(TestClass.PublicProperty));

        value.Should().Be(100);
    }

    [Fact]
    public void GetCachedPropertyValue_WhenObjectIsNull_ShouldReturnNull()
    {
        var value = ReflectionCache.GetCachedPropertyValue(null!, "AnyProperty");

        value.Should().BeNull();
    }

    [Fact]
    public void ClearCache_ShouldClearAllCaches()
    {
        _ = ReflectionCache.GetCachedProperties(typeof(TestClass));
        _ = ReflectionCache.GetCachedProperty(typeof(TestClass), nameof(TestClass.PublicProperty));

        GetCacheCount("_propertyCache").Should().BeGreaterThan(0);
        GetCacheCount("_propertyByNameCache").Should().BeGreaterThan(0);

        ReflectionCache.ClearCache();

        GetCacheCount("_propertyCache").Should().Be(0);
        GetCacheCount("_propertyByNameCache").Should().Be(0);
    }

    private static int GetCacheCount(string fieldName)
    {
        var field = typeof(ReflectionCache).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull();

        var value = field!.GetValue(null);
        value.Should().NotBeNull();

        var countProperty = value!.GetType().GetProperty("Count");
        countProperty.Should().NotBeNull();

        return (int)countProperty!.GetValue(value)!;
    }

    protected override void Cleanup()
    {
        ReflectionCache.ClearCache();
        base.Cleanup();
    }
}


[Trait("Category", TestCategories.Unit)]
public class GPUPowerInfoCacheTests : UnitTestBase
{
    [Fact]
    public void Constructor_DefaultValues_ShouldInitializeCorrectly()
    {
        var cache = new GPUPowerInfoCache();

        var (wattage, voltage) = cache.GetCached();

        wattage.Should().Be(-1);
        voltage.Should().Be(0);
    }

    [Fact]
    public void Update_ShouldSetCachedValues()
    {
        var cache = new GPUPowerInfoCache();

        cache.Update(100, 1.2);
        var (wattage, voltage) = cache.GetCached();

        wattage.Should().Be(100);
        voltage.Should().Be(1.2);
    }

    [Fact]
    public void IsCacheValid_WhenNoUpdate_ShouldReturnFalse()
    {
        var cache = new GPUPowerInfoCache();

        cache.IsCacheValid().Should().BeFalse();
    }

    [Fact]
    public void IsCacheValid_AfterUpdate_ShouldReturnTrue()
    {
        var cache = new GPUPowerInfoCache();
        cache.Update(100, 1.2);

        cache.IsCacheValid().Should().BeTrue();
    }

    [Fact]
    public void IsCacheValid_AfterExpiration_ShouldReturnFalse()
    {
        var cache = new GPUPowerInfoCache(TimeSpan.FromMilliseconds(10));
        cache.Update(100, 1.2);

        System.Threading.Thread.Sleep(20);

        cache.IsCacheValid().Should().BeFalse();
    }

    [Fact]
    public void ShouldTryNvidiaSmi_Initially_ShouldReturnTrue()
    {
        var cache = new GPUPowerInfoCache();

        cache.ShouldTryNvidiaSmi().Should().BeTrue();
    }

    [Fact]
    public void ShouldTryNvidiaSmi_AfterFailure_ShouldReturnFalse()
    {
        var cache = new GPUPowerInfoCache();

        cache.MarkNvidiaSmiFailed();

        cache.ShouldTryNvidiaSmi().Should().BeFalse();
    }

    [Fact]
    public void ShouldTryNvidiaSmi_AfterRetryInterval_ShouldReturnTrue()
    {
        var cache = new GPUPowerInfoCache(nvidiaSmiRetryInterval: TimeSpan.FromMilliseconds(10));

        cache.MarkNvidiaSmiFailed();
        System.Threading.Thread.Sleep(20);

        cache.ShouldTryNvidiaSmi().Should().BeTrue();
    }

    [Fact]
    public void ResetNvidiaSmiFailed_ShouldAllowRetry()
    {
        var cache = new GPUPowerInfoCache();

        cache.MarkNvidiaSmiFailed();
        cache.ResetNvidiaSmiFailed();

        cache.ShouldTryNvidiaSmi().Should().BeTrue();
    }
}
