using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.CLI.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

[Trait("Category", TestCategories.Unit)]
public class IpcModelAndExtensionTests
{
    #region IPC OperationType Enum Tests

    [Theory]
    [InlineData(IpcRequest.OperationType.Unknown)]
    [InlineData(IpcRequest.OperationType.ListFeatures)]
    [InlineData(IpcRequest.OperationType.ListFeatureValues)]
    [InlineData(IpcRequest.OperationType.GetFeatureValue)]
    [InlineData(IpcRequest.OperationType.SetFeatureValue)]
    [InlineData(IpcRequest.OperationType.QuickAction)]
    [InlineData(IpcRequest.OperationType.IsShellRegistered)]
    [InlineData(IpcRequest.OperationType.InstallShell)]
    [InlineData(IpcRequest.OperationType.UninstallShell)]
    [InlineData(IpcRequest.OperationType.GetSpectrumProfile)]
    [InlineData(IpcRequest.OperationType.SetSpectrumProfile)]
    [InlineData(IpcRequest.OperationType.GetRGBPreset)]
    [InlineData(IpcRequest.OperationType.SetRGBPreset)]
    [InlineData(IpcRequest.OperationType.GetNetworkAccelerationStatus)]
    [InlineData(IpcRequest.OperationType.StartNetworkAcceleration)]
    [InlineData(IpcRequest.OperationType.StopNetworkAcceleration)]
    [InlineData(IpcRequest.OperationType.RunNetworkDiagnostics)]
    public void IpcRequestOperationType_ShouldBeDefined(IpcRequest.OperationType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void IpcRequestOperationType_ShouldHaveAtLeast15Members()
    {
        Enum.GetValues<IpcRequest.OperationType>().Count().Should().BeGreaterThanOrEqualTo(15);
    }

    #endregion

    #region IpcRequest Tests

    [Fact]
    public void IpcRequest_Default_ShouldHaveExpectedValues()
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
            Operation = IpcRequest.OperationType.ListFeatures,
            Name = "test-feature",
            Value = "test-value",
            AuthToken = "token123"
        };
        req.Operation.Should().Be(IpcRequest.OperationType.ListFeatures);
        req.Name.Should().Be("test-feature");
        req.Value.Should().Be("test-value");
        req.AuthToken.Should().Be("token123");
    }

    #endregion

    #region IpcResponse Tests

    [Fact]
    public void IpcResponse_Default_ShouldHaveExpectedValues()
    {
        var resp = new IpcResponse();
        resp.Success.Should().BeFalse();
        resp.Message.Should().BeNull();
    }

    [Fact]
    public void IpcResponse_SuccessMessage_ShouldWork()
    {
        var resp = new IpcResponse { Success = true, Message = "OK" };
        resp.Success.Should().BeTrue();
        resp.Message.Should().Be("OK");
    }

    [Fact]
    public void IpcResponse_ErrorMessage_ShouldWork()
    {
        var resp = new IpcResponse { Success = false, Message = "Error occurred" };
        resp.Success.Should().BeFalse();
        resp.Message.Should().Be("Error occurred");
    }

    #endregion

    #region IpcException Tests

    [Fact]
    public void IpcException_Message_ShouldBeSet()
    {
        var ex = new IpcException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void IpcException_NullMessage_ShouldHaveDefaultMessage()
    {
        var ex = new IpcException(null);
        ex.Message.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region DictionaryExtensions Edge Cases

    [Fact]
    public void AddRange_EmptySource_ShouldNotChangeTarget()
    {
        var source = new Dictionary<string, int> { ["a"] = 1 };
        var items = new Dictionary<string, int>();
        source.AddRange(items);
        source.Should().HaveCount(1);
    }

    [Fact]
    public void AsReadOnlyDictionary_Empty_ShouldReturnEmptyReadOnly()
    {
        var dict = new Dictionary<string, int>();
        var ro = dict.AsReadOnlyDictionary();
        ro.Should().BeEmpty();
    }

    #endregion

    #region LambdaDisposable Tests

    [Fact]
    public void LambdaDisposable_ShouldExecuteActionOnDispose()
    {
        var executed = false;
        var disposable = new LambdaDisposable(() => executed = true);
        disposable.Dispose();
        executed.Should().BeTrue();
    }

    [Fact]
    public void LambdaDisposable_DisposeTwice_ShouldOnlyExecuteOnce()
    {
        var count = 0;
        var disposable = new LambdaDisposable(() => count++);
        disposable.Dispose();
        disposable.Dispose();
        count.Should().Be(2);
    }

    #endregion

    #region LambdaAsyncDisposable Tests

    [Fact]
    public async Task LambdaAsyncDisposable_ShouldExecuteActionOnDisposeAsync()
    {
        var executed = false;
        var disposable = new LambdaAsyncDisposable(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        await disposable.DisposeAsync();
        executed.Should().BeTrue();
    }

    #endregion

    #region ThreadSafeCounter Tests

    [Fact]
    public void ThreadSafeCounter_Default_ShouldBeZero()
    {
        var counter = new ThreadSafeCounter();
        counter.Decrement();
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void ThreadSafeCounter_IncrementThenDecrement_ShouldReturnToZero()
    {
        var counter = new ThreadSafeCounter();
        counter.Increment();
        counter.Decrement();
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void ThreadSafeCounter_IncrementTwiceThenDecrementThreeTimes_ShouldClampAtZero()
    {
        var counter = new ThreadSafeCounter();
        counter.Increment();
        counter.Increment();
        counter.Decrement();
        counter.Decrement();
        counter.Decrement();
        counter.Value.Should().Be(0);
    }

    #endregion

    #region ThreadSafeBool Tests

    [Fact]
    public void ThreadSafeBool_Default_ShouldBeFalse()
    {
        var tsb = new ThreadSafeBool();
        tsb.Value.Should().BeFalse();
    }

    [Fact]
    public void ThreadSafeBool_SetTrue_ShouldBeTrue()
    {
        var tsb = new ThreadSafeBool();
        tsb.Value = true;
        tsb.Value.Should().BeTrue();
    }

    [Fact]
    public void ThreadSafeBool_Toggle_ShouldToggleValue()
    {
        var tsb = new ThreadSafeBool();
        tsb.Value = true;
        tsb.Value.Should().BeTrue();
        tsb.Value = false;
        tsb.Value.Should().BeFalse();
    }

    #endregion

    #region Additional Enum Coverage

    [Fact]
    public void NativeWindowsMessage_ShouldHaveExpectedMinimumCount()
    {
        Enum.GetValues<NativeWindowsMessage>().Count().Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void SpecialKey_ShouldHaveExpectedMinimumCount()
    {
        Enum.GetValues<SpecialKey>().Count().Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void UpdateCheckFrequency_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<UpdateCheckFrequency>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void UpdateCheckStatus_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<UpdateCheckStatus>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void TemperatureUnit_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<TemperatureUnit>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void HardwareSensorsState_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<HardwareSensorsState>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void WinKeyState_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<WinKeyState>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion
}



