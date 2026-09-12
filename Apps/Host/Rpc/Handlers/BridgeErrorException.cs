using System;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Thrown by handlers to produce a structured bridge error response.
/// </summary>
public sealed class BridgeErrorException : Exception
{
    public int Code { get; }

    public BridgeErrorException(int code, string message)
        : base(message)
    {
        Code = code;
    }
}
