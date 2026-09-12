namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Bridge error codes shared by every RPC handler.
///
/// The -32xxx range follows JSON-RPC 2.0 conventions (protocol-level errors);
/// the -1xxx range carries application-level conditions the Electron renderer
/// maps to localized messages (see renderer api/bridge.ts localizeHostError).
/// Keep this list in sync with that mapping when adding codes.
/// </summary>
public static class BridgeErrorCodes
{
    // Protocol-level (JSON-RPC 2.0 reserved range).
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int UnknownMethod = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int RequestCancelled = -32800;

    /// <summary>Whole domain is unavailable on this platform (portable stubs).</summary>
    public const int PlatformNotSupported = -32099;

    /// <summary>A single stdio frame exceeded <see cref="BridgeRpcServer.MaxFrameBytes"/>.</summary>
    public const int RequestTooLarge = -32098;

    /// <summary>In-flight plus queued requests exceeded <see cref="BridgeRpcServer.MaxPendingRequests"/>.</summary>
    public const int TooManyRequests = -32097;

    /// <summary>God Mode is not supported by this device generation.</summary>
    public const int GodModeUnsupported = -32001;

    // Application-level conditions.
    public const int FeatureNotSupported = -1001;
    public const int AcPowerRequired = -1002;
    public const int UndefinedState = -1004;
    public const int MacroHooksFailed = -1005;
    public const int ElevationRequired = -1006;
    public const int NetworkProxyMissing = -1010;
    public const int NetworkHostsModeRefused = -1011;
    public const int NetworkStartRefused = -1012;
}
