using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Listeners;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Listeners;

[Trait("Category", TestCategories.Unit)]
public class AbstractWMIListenerTests
{
    [Fact]
    public async Task EventsShouldBeHandledSequentiallyPerListenerInstance()
    {
        var registration = new HandlerRegistration();
        using var listener = new SequencedTestListener(registration);

        await listener.StartAsync();

        registration.Emit(1);
        await listener.FirstEventEntered.Task;

        registration.Emit(2);
        await Task.Delay(100);

        listener.SecondEventEntered.Task.IsCompleted.Should().BeFalse();

        listener.AllowFirstEventToFinish.TrySetResult();
        await listener.Completed;

        listener.HandledValues.Should().Equal(1, 2);
    }

    private sealed class SequencedTestListener(HandlerRegistration registration) : AbstractWMIListener<EventArgs, int, int>(registration.Register)
    {
        private readonly List<int> _handledValues = [];
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<int> HandledValues => _handledValues;

        public TaskCompletionSource FirstEventEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowFirstEventToFinish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondEventEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completed => _completed.Task;

        protected override int GetValue(int value) => value;

        protected override EventArgs GetEventArgs(int value) => EventArgs.Empty;

        protected override async Task OnChangedAsync(int value)
        {
            if (value == 1)
            {
                FirstEventEntered.TrySetResult();
                await AllowFirstEventToFinish.Task;
            }
            else if (value == 2)
            {
                SecondEventEntered.TrySetResult();
            }

            _handledValues.Add(value);

            if (_handledValues.SequenceEqual([1, 2]))
                _completed.TrySetResult();
        }
    }

    private sealed class HandlerRegistration
    {
        private Action<int>? _handler;

        public Task<IDisposable> Register(Action<int> handler)
        {
            _handler = handler;
            return Task.FromResult<IDisposable>(new Registration(() => _handler = null));
        }

        public void Emit(int value)
        {
            _handler.Should().NotBeNull();
            _handler!(value);
        }

        private sealed class Registration(Action unregister) : IDisposable
        {
            public void Dispose() => unregister();
        }
    }
}
