using System.Net;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.NetworkProxy.Host;
using UniversalDeviceToolkit.NetworkProxy.Ipc;
using UniversalDeviceToolkit.Tests;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

[Trait("Category", "Fast")]
public sealed class NetworkProxyIpcContractTests
{
    [Fact]
    public void SessionToken_ShouldUseUrlSafeRandomFormat()
    {
        var token = NetworkProxySessionToken.Create();

        token.Length.Should().BeGreaterThanOrEqualTo(NetworkProxySessionToken.MinimumLength);
        NetworkProxySessionToken.IsValidFormat(token).Should().BeTrue();
        NetworkProxySessionToken.Matches(token, token).Should().BeTrue();
        NetworkProxySessionToken.Matches(token, NetworkProxySessionToken.Create()).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not valid token")]
    [InlineData("short")]
    public void SessionToken_ShouldRejectInvalidValues(string? token)
    {
        NetworkProxySessionToken.IsValidFormat(token).Should().BeFalse();
    }

    [Fact]
    public void ArgumentResolvers_ShouldUseSafeDefaults()
    {
        NetworkProxyIpcServer.ResolvePipeName([]).Should().Be(NetworkAccelerationDefaults.DefaultPipeName);
        NetworkProxyIpcServer.ResolveListenPort([]).Should().Be(NetworkAccelerationDefaults.DefaultListenPort);
        NetworkProxyIpcServer.ResolveListenPort(["--port", "0"]).Should().Be(NetworkAccelerationDefaults.DefaultListenPort);
        NetworkProxyIpcServer.ResolveListenPort(["--port", "65536"]).Should().Be(NetworkAccelerationDefaults.DefaultListenPort);
    }

    [Fact]
    public void SessionResolver_ShouldPreferAndClearEnvironmentToken()
    {
        var token = NetworkProxySessionToken.Create();
        using var scope = new EnvironmentVariableScope(
            NetworkProxySessionToken.WorkerTokenEnvironmentVariable,
            token);

        NetworkProxyIpcServer.ResolveSessionToken(["--token", "legacy-token"]).Should().Be(token);
        Environment.GetEnvironmentVariable(NetworkProxySessionToken.WorkerTokenEnvironmentVariable)
            .Should().BeNull();
    }

    [Fact]
    public void Server_ShouldRejectInvalidSessionToken()
    {
        var action = () => new NetworkProxyIpcServer(
            "udt-test-pipe",
            "invalid token",
            new NoopProxyHost());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task LocalProxyHost_ShouldStartOnLoopbackAndStopCleanly()
    {
        await using var host = new LocalHttpProxyHost(0);

        await host.StartAsync();

        host.IsRunning.Should().BeTrue();
        host.ListenPort.Should().BeInRange(1, 65535);
        host.StatusSummary.Should().Contain("loopback");

        await host.StopAsync();

        host.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task LocalProxyHost_ShouldReportConnectionAndDestinationTelemetry()
    {
        var remote = new TcpListener(IPAddress.Loopback, 0);
        remote.Start();
        var remotePort = ((IPEndPoint)remote.LocalEndpoint).Port;
        var remoteTask = Task.Run(async () =>
        {
            using var serverClient = await remote.AcceptTcpClientAsync();
            await using var stream = serverClient.GetStream();
            var buffer = new byte[4096];
            _ = await stream.ReadAsync(buffer);
            var response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 5\r\n\r\nhello");
            await stream.WriteAsync(response);
            remote.Stop();
        });

        await using var host = new LocalHttpProxyHost(0);
        host.SetDomainAllowlist(new[] { "127.0.0.1" });
        await host.StartAsync();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, host.ListenPort);
            await using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes(
                $"GET http://127.0.0.1:{remotePort}/ HTTP/1.1\r\nHost: 127.0.0.1:{remotePort}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(request);
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var responseText = await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5));
            responseText.Should().Contain("200 OK");
            await remoteTask.WaitAsync(TimeSpan.FromSeconds(5));

            var connection = host.GetConnectionSnapshots(10).Should().ContainSingle().Subject;
            connection.Host.Should().Be("127.0.0.1");
            connection.Port.Should().Be(remotePort);
            connection.Protocol.Should().Be("HTTP");
            connection.State.Should().Be("completed");
            connection.BytesUploaded.Should().BeGreaterThan(0);
            connection.BytesDownloaded.Should().BeGreaterThan(0);
            connection.ConnectLatencyMs.Should().NotBeNull();

            var destination = host.GetDestinationSnapshots(10).Should().ContainSingle().Subject;
            destination.Host.Should().Be("127.0.0.1");
            destination.Port.Should().Be(remotePort);
            destination.TotalConnections.Should().Be(1);
            destination.ActiveConnections.Should().Be(0);
            destination.BytesUploaded.Should().BeGreaterThan(0);
            destination.BytesDownloaded.Should().BeGreaterThan(0);
            destination.LastState.Should().Be("completed");
        }
        finally
        {
            remote.Stop();
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task LocalProxyHost_ShouldRecordBlockedConnections()
    {
        await using var host = new LocalHttpProxyHost(0);
        host.SetDomainAllowlist(new[] { "allowed.example" });
        await host.StartAsync();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, host.ListenPort);
            await using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes(
                "CONNECT denied.example:443 HTTP/1.1\r\nHost: denied.example\r\n\r\n");
            await stream.WriteAsync(request);
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var responseText = await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5));
            responseText.Should().Contain("403 Forbidden");

            var connection = host.GetConnectionSnapshots(10).Should().ContainSingle().Subject;
            connection.Host.Should().Be("denied.example");
            connection.State.Should().Be("blocked");
            connection.Error.Should().Contain("allowlist");

            var destination = host.GetDestinationSnapshots(10).Should().ContainSingle().Subject;
            destination.LastState.Should().Be("blocked");
            destination.ActiveConnections.Should().Be(0);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task IpcServer_ShouldReturnEmptyTrafficWhenHostDoesNotExposeSnapshots()
    {
        var pipeName = $"udt-fast-{Guid.NewGuid():N}";
        var token = NetworkProxySessionToken.Create();
        await using var server = new NetworkProxyIpcServer(pipeName, token, new NoopProxyHost());
        using var cancellation = new CancellationTokenSource();
        var serverTask = server.RunAsync(cancellation.Token);

        try
        {
            using (var status = await SendAsync(pipeName, token, "status"))
            {
                status.RootElement.GetProperty("data").GetProperty("bytesUploaded").GetString().Should().Be("0");
                status.RootElement.GetProperty("data").GetProperty("bytesDownloaded").GetString().Should().Be("0");
                status.RootElement.GetProperty("data").GetProperty("activeConnections").GetString().Should().Be("0");
                status.RootElement.GetProperty("data").GetProperty("totalConnections").GetString().Should().Be("0");
            }

            using (var connections = await SendAsync(pipeName, token, "connections"))
            {
                JsonDocument.Parse(connections.RootElement.GetProperty("data").GetProperty("items").GetString()!)
                    .RootElement.GetArrayLength().Should().Be(0);
            }

            using (var destinations = await SendAsync(pipeName, token, "destinations"))
            {
                JsonDocument.Parse(destinations.RootElement.GetProperty("data").GetProperty("items").GetString()!)
                    .RootElement.GetArrayLength().Should().Be(0);
            }
        }
        finally
        {
            cancellation.Cancel();
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task IpcServer_ShouldStopWhenCanceledBeforeFirstConnection()
    {
        var pipeName = $"udt-fast-{Guid.NewGuid():N}";
        await using var server = new NetworkProxyIpcServer(
            pipeName,
            NetworkProxySessionToken.Create(),
            new NoopProxyHost());
        using var cancellation = new CancellationTokenSource();
        var serverTask = server.RunAsync(cancellation.Token);

        cancellation.Cancel();

        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        serverTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task IpcServer_ShouldEnforceAuthorizationAndDispatchLifecycleOperations()
    {
        var pipeName = $"udt-fast-{Guid.NewGuid():N}";
        var token = NetworkProxySessionToken.Create();
        var host = new RecordingProxyHost();
        await using var server = new NetworkProxyIpcServer(pipeName, token, host);
        using var cancellation = new CancellationTokenSource();
        var serverTask = server.RunAsync(cancellation.Token);

        try
        {
            using (var unauthorized = await SendAsync(pipeName, "bad-token", "status"))
            {
                unauthorized.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
                unauthorized.RootElement.GetProperty("message").GetString().Should().Be("unauthorized");
            }

            using (var status = await SendAsync(pipeName, token, "status"))
            {
                status.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
                status.RootElement.GetProperty("data").GetProperty("running").GetString().Should().Be("false");
                status.RootElement.GetProperty("data").GetProperty("health").GetString().Should().Be("stopped");
                status.RootElement.GetProperty("data").GetProperty("bytesUploaded").GetString().Should().Be("12");
                status.RootElement.GetProperty("data").GetProperty("bytesDownloaded").GetString().Should().Be("34");
                status.RootElement.GetProperty("data").GetProperty("activeConnections").GetString().Should().Be("0");
                status.RootElement.GetProperty("data").GetProperty("totalConnections").GetString().Should().Be("1");
            }

            using (var connections = await SendAsync(pipeName, token, "connections"))
            {
                connections.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
                using var items = JsonDocument.Parse(
                    connections.RootElement.GetProperty("data").GetProperty("items").GetString()!);
                items.RootElement.GetArrayLength().Should().Be(1);
                items.RootElement[0].GetProperty("host").GetString().Should().Be("example.com");
                items.RootElement[0].GetProperty("port").GetInt32().Should().Be(443);
                items.RootElement[0].GetProperty("protocol").GetString().Should().Be("CONNECT");
                items.RootElement[0].GetProperty("state").GetString().Should().Be("completed");
            }

            using (var destinations = await SendAsync(pipeName, token, "destinations"))
            {
                destinations.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
                using var items = JsonDocument.Parse(
                    destinations.RootElement.GetProperty("data").GetProperty("items").GetString()!);
                items.RootElement.GetArrayLength().Should().Be(1);
                items.RootElement[0].GetProperty("host").GetString().Should().Be("example.com");
                items.RootElement[0].GetProperty("port").GetInt32().Should().Be(443);
                items.RootElement[0].GetProperty("lastState").GetString().Should().Be("completed");
                items.RootElement[0].GetProperty("totalConnections").GetInt64().Should().Be(1);
            }

            using (var invalidRules = await SendAsync(pipeName, token, "rules", "not-json"))
            {
                invalidRules.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
                invalidRules.RootElement.GetProperty("message").GetString()
                    .Should().Be("invalid rules json (expected string array)");
            }

            var rulesJson = JsonSerializer.Serialize(new[] { "example.com", "localhost" });
            using (var rules = await SendAsync(pipeName, token, "rules", rulesJson))
            {
                rules.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
                rules.RootElement.GetProperty("data").GetProperty("rules").GetString().Should().Be(rulesJson);
            }

            host.LastAllowlist.Should().BeEquivalentTo(new[] { "example.com", "localhost" });

            using (var start = await SendAsync(pipeName, token, "start"))
            {
                start.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            }

            host.StartCalls.Should().Be(1);
            host.IsRunning.Should().BeTrue();

            using (var stop = await SendAsync(pipeName, token, "stop"))
            {
                stop.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            }

            host.StopCalls.Should().Be(1);
            host.IsRunning.Should().BeFalse();

            using (var unknown = await SendAsync(pipeName, token, "unsupported"))
            {
                unknown.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
                unknown.RootElement.GetProperty("message").GetString().Should().Be("unknown operation: unsupported");
            }
        }
        finally
        {
            cancellation.Cancel();
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task<JsonDocument> SendAsync(
        string pipeName,
        string token,
        string operation,
        string? payload = null)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(connectTimeout.Token);

        await using var writer = new StreamWriter(client, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(
            client,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        var request = JsonSerializer.Serialize(new
        {
            operation,
            token,
            payload
        });
        using var writeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await writer.WriteLineAsync(request.AsMemory(), writeTimeout.Token);

        var response = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        response.Should().NotBeNullOrWhiteSpace();
        return JsonDocument.Parse(response!);
    }

    private sealed class NoopProxyHost : INetworkProxyHost
    {
        public bool IsRunning => false;
        public int ListenPort => 0;
        public string StatusSummary => "stopped";
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void SetDomainAllowlist(IReadOnlyList<string>? domains)
        {
        }
    }

    private sealed class RecordingProxyHost : INetworkProxyHost, INetworkProxyTrafficSource
    {
        private readonly NetworkProxyConnectionSnapshot[] _connections =
        [
            new()
            {
                Id = 1,
                Host = "example.com",
                Port = 443,
                Protocol = "CONNECT",
                State = "completed",
                BytesUploaded = 12,
                BytesDownloaded = 34,
                StartedAtUtc = DateTime.UnixEpoch,
            }
        ];

        private readonly NetworkProxyDestinationSnapshot[] _destinations =
        [
            new()
            {
                Host = "example.com",
                Port = 443,
                TotalConnections = 1,
                ActiveConnections = 0,
                BytesUploaded = 12,
                BytesDownloaded = 34,
                LastState = "completed",
                LastUpdatedAtUtc = DateTime.UnixEpoch,
            }
        ];

        public bool IsRunning { get; private set; }
        public int ListenPort => 3128;
        public string StatusSummary => IsRunning ? "running" : "stopped";
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public IReadOnlyList<string>? LastAllowlist { get; private set; }
        public long BytesUploaded => 12;
        public long BytesDownloaded => 34;
        public int ActiveConnections => 0;
        public long TotalConnections => 1;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalls++;
            IsRunning = false;
            return Task.CompletedTask;
        }

        public void SetDomainAllowlist(IReadOnlyList<string>? domains)
        {
            LastAllowlist = domains?.ToArray();
        }

        public IReadOnlyList<NetworkProxyConnectionSnapshot> GetConnectionSnapshots(int maxItems = 40) =>
            _connections.Take(maxItems).ToArray();

        public IReadOnlyList<NetworkProxyDestinationSnapshot> GetDestinationSnapshots(int maxItems = 40) =>
            _destinations.Take(maxItems).ToArray();
    }
}
