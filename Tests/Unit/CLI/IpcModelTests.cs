using System;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.CLI.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Trait("Category", TestCategories.Unit)]
public class IpcModelTests
{
    #region IpcRequest Tests

    [Fact]
    public void IpcRequest_Defaults_ShouldBeNull()
    {
        var req = new IpcRequest();
        req.Operation.Should().BeNull();
        req.Name.Should().BeNull();
        req.Value.Should().BeNull();
        req.AuthToken.Should().BeNull();
    }

    [Fact]
    public void IpcRequest_SetProperties_ShouldRetainValues()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetFeatureValue,
            Name = "ThermalMode",
            Value = "Performance",
            AuthToken = "secret-token"
        };
        req.Operation.Should().Be(IpcRequest.OperationType.GetFeatureValue);
        req.Name.Should().Be("ThermalMode");
        req.Value.Should().Be("Performance");
        req.AuthToken.Should().Be("secret-token");
    }

    [Theory]
    [InlineData(IpcRequest.OperationType.Unknown)]
    [InlineData(IpcRequest.OperationType.ListFeatures)]
    [InlineData(IpcRequest.OperationType.ListFeatureValues)]
    [InlineData(IpcRequest.OperationType.ListQuickActions)]
    [InlineData(IpcRequest.OperationType.GetFeatureValue)]
    [InlineData(IpcRequest.OperationType.SetFeatureValue)]
    [InlineData(IpcRequest.OperationType.GetSpectrumProfile)]
    [InlineData(IpcRequest.OperationType.SetSpectrumProfile)]
    [InlineData(IpcRequest.OperationType.GetSpectrumBrightness)]
    [InlineData(IpcRequest.OperationType.SetSpectrumBrightness)]
    [InlineData(IpcRequest.OperationType.GetRGBPreset)]
    [InlineData(IpcRequest.OperationType.SetRGBPreset)]
    [InlineData(IpcRequest.OperationType.QuickAction)]
    [InlineData(IpcRequest.OperationType.IsShellRegistered)]
    [InlineData(IpcRequest.OperationType.IsShellInstalled)]
    [InlineData(IpcRequest.OperationType.InstallShell)]
    [InlineData(IpcRequest.OperationType.UninstallShell)]
    [InlineData(IpcRequest.OperationType.GetAppStatus)]
    [InlineData(IpcRequest.OperationType.GetNetworkAccelerationStatus)]
    [InlineData(IpcRequest.OperationType.StartNetworkAcceleration)]
    [InlineData(IpcRequest.OperationType.StopNetworkAcceleration)]
    [InlineData(IpcRequest.OperationType.RunNetworkDiagnostics)]
    public void IpcRequest_OperationType_AllValues_ShouldBeDefined(IpcRequest.OperationType op)
    {
        Enum.IsDefined(op).Should().BeTrue();
    }

    [Fact]
    public void IpcRequest_OperationType_Has22Values()
    {
        Enum.GetValues<IpcRequest.OperationType>().Should().HaveCount(22);
    }

    #endregion

    #region IpcResponse Tests

    [Fact]
    public void IpcResponse_Defaults_ShouldBeNull()
    {
        var resp = new IpcResponse();
        resp.Success.Should().BeFalse();
        resp.Message.Should().BeNull();
    }

    [Fact]
    public void IpcResponse_SetSuccess_ShouldRetainValue()
    {
        var resp = new IpcResponse { Success = true, Message = "OK" };
        resp.Success.Should().BeTrue();
        resp.Message.Should().Be("OK");
    }

    [Fact]
    public void IpcResponse_SetFailure_ShouldRetainValue()
    {
        var resp = new IpcResponse { Success = false, Message = "Not found" };
        resp.Success.Should().BeFalse();
        resp.Message.Should().Be("Not found");
    }

    [Fact]
    public void IpcResponse_SerializeRoundtrip_ShouldPreserveData()
    {
        var resp = new IpcResponse { Success = true, Message = "test" };
        var json = JsonSerializer.Serialize(resp);
        var deserialized = JsonSerializer.Deserialize<IpcResponse>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Success.Should().BeTrue();
        deserialized.Message.Should().Be("test");
    }

    #endregion

    #region IpcException Tests

    [Fact]
    public void IpcException_Message_ShouldRetainValue()
    {
        var ex = new IpcException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void IpcException_NullMessage_ShouldNotThrow()
    {
        var act = () => new IpcException(null);
        act.Should().NotThrow();
    }

    [Fact]
    public void IpcException_ShouldBeException()
    {
        new IpcException("x").Should().BeAssignableTo<Exception>();
    }

    #endregion

    #region IpcConnectException Tests

    [Fact]
    public void IpcConnectException_ShouldBeException()
    {
        new IpcConnectException().Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void IpcConnectException_DefaultMessage_ShouldNotBeNull()
    {
        var ex = new IpcConnectException();
        ex.Message.Should().NotBeNullOrEmpty();
    }

    #endregion
}
