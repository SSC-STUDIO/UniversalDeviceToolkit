using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Always-on capability probe so the Electron client can hide vendor routes
/// instead of treating portable Host answers as generic failures.
/// </summary>
internal static class PortableCapabilityHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("host.getCapabilities", async (_, cancellationToken) =>
        {
            try
            {
                var manifest = await PortableCapabilityManifest.BuildAsync(cancellationToken)
                    .ConfigureAwait(false);
                return BridgeResult.Ok(manifest);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.InternalError,
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        });
    }
}
