using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;

namespace UniversalDeviceToolkit.Lib.Features;

public abstract class AbstractUEFIFeature<T>(string guid, string scopeName, uint scopeAttribute)
    : IFeature<T> where T : struct, Enum, IComparable
{
    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _ = await GetStateAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"feature-uefi-supported-{GetType().Name}",
                $"UEFI feature support probe failed for {GetType().Name}.",
                ex);
            return false;
        }
    }

    public Task<T[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<T>());
    }

    public abstract Task<T> GetStateAsync(CancellationToken cancellationToken = default);

    public abstract Task SetStateAsync(T state, CancellationToken cancellationToken = default);

    public virtual void InvalidateResolution()
    {
    }

    protected unsafe Task<TS> ReadFromUefiAsync<TS>(CancellationToken cancellationToken = default) where TS : struct => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Reading from UEFI... [feature={GetType().Name}]");

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TS>());

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                Log.Instance.Warning($"Cannot set UEFI privileges [feature={GetType().Name}]");

                throw ExceptionHelper.CannotSetUEFIPrivileges();
            }

            var ptrSize = (uint)Marshal.SizeOf<TS>();
            fixed (char* scopeNamePtr = scopeName)
            fixed (char* guidPtr = guid)
            {
                if (PInvoke.GetFirmwareEnvironmentVariableEx(scopeNamePtr, guidPtr, ptr.ToPointer(), ptrSize, null) != 0)
                {
                    var result = Marshal.PtrToStructure<TS>(ptr);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Read from UEFI successful [feature={GetType().Name}]");

                    return result;
                }
                else
                {
                    Log.Instance.Warning($"Cannot read variable {scopeName} from UEFI [feature={GetType().Name}]");

                    throw ExceptionHelper.CannotReadUEFIVariable(scopeName);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    }, cancellationToken);

    protected unsafe Task WriteToUefiAsync<TS>(TS structure, CancellationToken cancellationToken = default) where TS : struct => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TS>());

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                Log.Instance.Warning($"Cannot set UEFI privileges [feature={GetType().Name}]");

                throw ExceptionHelper.CannotSetUEFIPrivileges();
            }

            Marshal.StructureToPtr(structure, ptr, false);
            var ptrSize = (uint)Marshal.SizeOf<TS>();
            fixed (char* scopeNamePtr = scopeName)
            fixed (char* guidPtr = guid)
            {
                if (!PInvoke.SetFirmwareEnvironmentVariableEx(scopeNamePtr, guidPtr, ptr.ToPointer(), ptrSize, scopeAttribute))
                {
                    Log.Instance.Warning($"Cannot write variable {scopeName} to UEFI [feature={GetType().Name}]");

                    throw ExceptionHelper.CannotWriteUEFIVariable(scopeName);
                }
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"WriteAsync to UEFI successful [feature={GetType().Name}]");
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    }, cancellationToken);
}
