using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class StreamExtensionsTests
{
    [Fact]
    public async Task CopyToAsync_ShouldCopyAllBytes()
    {
        var sourceBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var source = new MemoryStream(sourceBytes);
        using var destination = new MemoryStream();

        await source.CopyToAsync(destination, 4);

        destination.ToArray().Should().BeEquivalentTo(sourceBytes);
    }

    [Fact]
    public async Task CopyToAsync_WithProgress_ShouldReportBytesRead()
    {
        var sourceBytes = new byte[20];
        Random.Shared.NextBytes(sourceBytes);
        using var source = new MemoryStream(sourceBytes);
        using var destination = new MemoryStream();
        var progressValues = new List<long>();

        await StreamExtensions.CopyToAsync(source, destination, 8, new Progress<long>(p => progressValues.Add(p)));

        progressValues.Should().NotBeEmpty();
        // Progress reports cumulative bytes; last value should equal total bytes copied
        destination.ToArray().Length.Should().Be(20);
    }

    [Fact]
    public async Task CopyToAsync_WithCancellationToken_ShouldThrowOnCancel()
    {
        using var source = new MemoryStream(new byte[1000]);
        using var destination = new MemoryStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await source.CopyToAsync(destination, 64, null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CopyToAsync_WithEmptySource_ShouldNotWrite()
    {
        using var source = new MemoryStream(Array.Empty<byte>());
        using var destination = new MemoryStream();

        await source.CopyToAsync(destination, 4);

        destination.Length.Should().Be(0);
    }

    [Fact]
    public async Task CopyToAsync_WithUnreadableSource_ShouldThrow()
    {
        var source = new NonReadableStream();
        using var destination = new MemoryStream();

        // Use static invocation to avoid shadowing by Stream.CopyToAsync(Stream, int)
        var act = async () => await StreamExtensions.CopyToAsync(source, destination, 4);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CopyToAsync_WithNonWritableDestination_ShouldThrow()
    {
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        var destination = new NonWritableStream();

        var act = async () => await StreamExtensions.CopyToAsync(source, destination, 4);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private class NonReadableStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    private class NonWritableStream : MemoryStream
    {
        public override bool CanWrite => false;
    }
}
