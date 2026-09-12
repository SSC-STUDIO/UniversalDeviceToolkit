using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Features;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features;

[Trait("Category", TestCategories.Unit)]
public class AbstractDriverFeatureTests
{
    [Fact]
    public async Task SetStateAsync_WhenVerificationNeverSucceeds_ShouldThrow()
    {
        // Arrange
        var feature = new TestDriverFeature(TestDriverState.Off);

        // Act
        Func<Task> act = () => feature.SetStateAsync(TestDriverState.On);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TestDriverFeature*On*");
    }

    [Fact]
    public async Task SetStateAsync_WhenVerificationEventuallySucceeds_ShouldComplete()
    {
        // Arrange
        var feature = new TestDriverFeature(TestDriverState.Off, TestDriverState.On);

        // Act
        await feature.SetStateAsync(TestDriverState.On);

        // Assert
        feature.CurrentState.Should().Be(TestDriverState.On);
    }

    private enum TestDriverState
    {
        Off,
        On,
    }

    private sealed class TestDriverFeature(params TestDriverState[] states)
        : AbstractDriverFeature<TestDriverState>(() => new SafeFileHandle(IntPtr.Zero, false), 0)
    {
        private readonly Queue<TestDriverState> _states = new(states);

        public TestDriverState CurrentState { get; private set; } = states.Length > 0 ? states[0] : TestDriverState.Off;

        public override Task<TestDriverState> GetStateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_states.Count > 0)
                CurrentState = _states.Dequeue();

            return Task.FromResult(CurrentState);
        }

        protected override Task<TestDriverState> FromInternalAsync(uint state, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((TestDriverState)state);
        }

        protected override uint GetInBufferValue() => 0;

        protected override Task<uint[]> ToInternalAsync(TestDriverState state, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Array.Empty<uint>());
        }
    }
}
