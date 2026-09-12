using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Serialization;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Serialization;

[Trait("Category", TestCategories.Unit)]
public class GPUOverclockInfoJsonConverterTests
{
    #region Read Tests

    [Fact]
    public void Read_WithValidJson_ShouldDeserialize()
    {
        var json = """{"CoreDeltaMhz":100,"MemoryDeltaMhz":200}""";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var result = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        result.CoreDeltaMhz.Should().Be(100);
        result.MemoryDeltaMhz.Should().Be(200);
    }

    [Fact]
    public void Read_WithCaseInsensitivePropertyNames_ShouldDeserialize()
    {
        var json = """{"coreDeltaMhz":50,"memoryDeltaMhz":75}""";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var result = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        result.CoreDeltaMhz.Should().Be(50);
        result.MemoryDeltaMhz.Should().Be(75);
    }

    [Fact]
    public void Read_WithEmptyObject_ShouldReturnDefault()
    {
        var json = """{}""";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var result = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        result.Should().Be(GPUOverclockInfo.Zero);
    }

    [Fact]
    public void Read_WithUnknownProperties_ShouldIgnore()
    {
        var json = """{"CoreDeltaMhz":10,"UnknownField":99,"MemoryDeltaMhz":20}""";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var result = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        result.CoreDeltaMhz.Should().Be(10);
        result.MemoryDeltaMhz.Should().Be(20);
    }

    [Fact]
    public void Read_WithNegativeValues_ShouldDeserialize()
    {
        var json = """{"CoreDeltaMhz":-50,"MemoryDeltaMhz":-100}""";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var result = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        result.CoreDeltaMhz.Should().Be(-50);
        result.MemoryDeltaMhz.Should().Be(-100);
    }

    [Fact]
    public void Read_WhenNotStartObject_ShouldThrow()
    {
        var json = """[1,2]""";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var act = () => JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);
        act.Should().Throw<JsonException>();
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_ShouldSerializeBothFields()
    {
        var info = new GPUOverclockInfo(150, 300);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var json = JsonSerializer.Serialize(info, options);

        json.Should().Contain("CoreDeltaMhz").And.Contain("MemoryDeltaMhz");
        json.Should().Contain("150").And.Contain("300");
    }

    [Fact]
    public void Write_WithZeroValues_ShouldSerialize()
    {
        var info = GPUOverclockInfo.Zero;
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var json = JsonSerializer.Serialize(info, options);

        json.Should().Contain("0");
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void Roundtrip_ShouldPreserveValues()
    {
        var original = new GPUOverclockInfo(123, 456);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void Roundtrip_WithNegativeValues_ShouldPreserveValues()
    {
        var original = new GPUOverclockInfo(-50, -75);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GPUOverclockInfoJsonConverter());

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<GPUOverclockInfo>(json, options);

        deserialized.Should().Be(original);
    }

    #endregion
}
