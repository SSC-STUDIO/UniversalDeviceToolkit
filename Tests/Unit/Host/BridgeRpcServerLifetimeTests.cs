using FluentAssertions;
using UniversalDeviceToolkit.Host.Rpc;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Unit)]
public sealed class BridgeRpcServerLifetimeTests
{
    [Fact]
    public void Dispose_ReleasesAllResourcesOnceEvenWhenOneFails()
    {
        using var server = new BridgeRpcServer(new MemoryStream(), new MemoryStream());
        var completed = new LifetimeResource();
        var failing = new LifetimeResource(fail: true);
        server.RegisterLifetimeResource(completed);
        server.RegisterLifetimeResource(failing);

        server.Dispose();
        server.Dispose();

        completed.DisposeCount.Should().Be(1);
        failing.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void RegisterResource_AfterDisposal_ReleasesRejectedResource()
    {
        using var server = new BridgeRpcServer(new MemoryStream(), new MemoryStream());
        var resource = new LifetimeResource();
        server.Dispose();

        var register = () => server.RegisterLifetimeResource(resource);

        register.Should().Throw<ObjectDisposedException>();
        resource.DisposeCount.Should().Be(1);
    }

    private sealed class LifetimeResource(bool fail = false) : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (fail)
                throw new InvalidOperationException("Resource release failure.");
        }
    }
}
