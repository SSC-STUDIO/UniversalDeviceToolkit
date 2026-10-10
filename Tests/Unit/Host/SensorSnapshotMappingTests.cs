using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class SensorSnapshotMappingTests
{
    [Fact]
    public void MemorySnapshot_PreservesZeroAndConvertsFractionalGigabytes()
    {
        var snapshot = JsonSerializer.SerializeToElement(SensorsHandlers.MapMemory(0, 1.5f, 16, 0));
        Assert.Equal(0, snapshot.GetProperty("usage").GetSingle());
        Assert.Equal(1536, snapshot.GetProperty("usedMb").GetSingle());
        Assert.Equal(16384, snapshot.GetProperty("totalMb").GetSingle());
        Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("highestTemperature").ValueKind);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void MemorySnapshot_UnavailableValuesRemainNull(float unavailable)
    {
        var snapshot = JsonSerializer.SerializeToElement(SensorsHandlers.MapMemory(unavailable, unavailable, unavailable, double.NaN));
        foreach (var field in snapshot.EnumerateObject())
            Assert.Equal(JsonValueKind.Null, field.Value.ValueKind);
    }
}
