using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Serialization;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Serialization;

[Trait("Category", TestCategories.Unit)]
public class LltJsonTests
{
    #region CreateSettingsOptions Tests

    [Fact]
    public void CreateSettingsOptions_ShouldReturnNonNullOptions()
    {
        var options = LltJson.CreateSettingsOptions();
        options.Should().NotBeNull();
    }

    [Fact]
    public void CreateSettingsOptions_ShouldWriteIndented()
    {
        var options = LltJson.CreateSettingsOptions();
        options.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void CreateSettingsOptions_ShouldHaveStringEnumConverter()
    {
        var options = LltJson.CreateSettingsOptions();
        options.Converters.Should().Contain(c => c is JsonStringEnumConverter);
    }

    [Fact]
    public void CreateSettingsOptions_ShouldHaveReplaceCreationHandling()
    {
        var options = LltJson.CreateSettingsOptions();
        options.PreferredObjectCreationHandling.Should().Be(JsonObjectCreationHandling.Replace);
    }

    [Fact]
    public void CreateSettingsOptions_ShouldHaveMaxDepth32()
    {
        var options = LltJson.CreateSettingsOptions();
        options.MaxDepth.Should().Be(32);
    }

    [Fact]
    public void CreateSettingsOptions_ShouldSerializeEnumAsString()
    {
        var options = LltJson.CreateSettingsOptions();
        var json = JsonSerializer.Serialize(AutorunState.Enabled, options);

        json.Should().Be("\"Enabled\"");
    }

    [Fact]
    public void CreateSettingsOptions_ShouldDeserializeEnumFromString()
    {
        var options = LltJson.CreateSettingsOptions();
        var result = JsonSerializer.Deserialize<AutorunState>("\"Disabled\"", options);

        result.Should().Be(AutorunState.Disabled);
    }

    [Fact]
    public void CreateSettingsOptions_ShouldProduceIndentedJson()
    {
        var options = LltJson.CreateSettingsOptions();
        var obj = new TestPayload { Name = "test", Value = 42 };
        var json = JsonSerializer.Serialize(obj, options);

        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }

    [Fact]
    public void CreateSettingsOptions_ShouldRoundTripObject()
    {
        var options = LltJson.CreateSettingsOptions();
        var original = new TestPayload { Name = "roundtrip", Value = 99 };

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<TestPayload>(json, options);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Value.Should().Be(original.Value);
    }

    #endregion

    #region CreateCompactOptions Tests

    [Fact]
    public void CreateCompactOptions_ShouldReturnNonNullOptions()
    {
        var options = LltJson.CreateCompactOptions();
        options.Should().NotBeNull();
    }

    [Fact]
    public void CreateCompactOptions_ShouldNotWriteIndented()
    {
        var options = LltJson.CreateCompactOptions();
        options.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void CreateCompactOptions_ShouldHaveStringEnumConverter()
    {
        var options = LltJson.CreateCompactOptions();
        options.Converters.Should().Contain(c => c is JsonStringEnumConverter);
    }

    [Fact]
    public void CreateCompactOptions_ShouldProduceCompactJson()
    {
        var options = LltJson.CreateCompactOptions();
        var obj = new TestPayload { Name = "compact", Value = 1 };
        var json = JsonSerializer.Serialize(obj, options);

        json.Should().NotContain("\n");
        json.Should().NotContain("  ");
    }

    [Fact]
    public void CreateCompactOptions_ShouldRoundTripObject()
    {
        var options = LltJson.CreateCompactOptions();
        var original = new TestPayload { Name = "ipc", Value = 7 };

        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<TestPayload>(json, options);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Value.Should().Be(original.Value);
    }

    #endregion

    #region Cross-option compatibility Tests

    [Fact]
    public void SettingsOptions_ShouldReadCompactJson()
    {
        var compactOptions = LltJson.CreateCompactOptions();
        var settingsOptions = LltJson.CreateSettingsOptions();
        var original = new TestPayload { Name = "cross", Value = 3 };

        var compactJson = JsonSerializer.Serialize(original, compactOptions);
        var deserialized = JsonSerializer.Deserialize<TestPayload>(compactJson, settingsOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Value.Should().Be(original.Value);
    }

    [Fact]
    public void CompactOptions_ShouldReadIndentedJson()
    {
        var compactOptions = LltJson.CreateCompactOptions();
        var settingsOptions = LltJson.CreateSettingsOptions();
        var original = new TestPayload { Name = "reverse", Value = 5 };

        var indentedJson = JsonSerializer.Serialize(original, settingsOptions);
        var deserialized = JsonSerializer.Deserialize<TestPayload>(indentedJson, compactOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Value.Should().Be(original.Value);
    }

    [Fact]
    public void SettingsOptions_ShouldSerializeNullProperty()
    {
        var options = LltJson.CreateSettingsOptions();
        var obj = new TestPayload { Name = null, Value = 0 };
        var json = JsonSerializer.Serialize(obj, options);

        json.Should().Contain("\"Name\": null");
    }

    #endregion

    #region Helper types

    private class TestPayload
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    #endregion
}
