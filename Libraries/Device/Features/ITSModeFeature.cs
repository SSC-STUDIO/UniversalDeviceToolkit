// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public partial class ITSModeFeature : IFeature<ITSMode>
{
    #region Constants and Imports
    private const uint DISPATCHER_VERSION_3 = 8192U;

    [LibraryImport("PowerBattery.dll", EntryPoint = "?SetITSMode@CIntelligentCooling@PowerBattery@@QEAAHAEAW4ITSMode@12@@Z", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int SetITSMode(ref CIntelligentCooling instance, ref ITSMode itsMode);

    [LibraryImport("PowerBattery.dll", EntryPoint = "?GetITSMode@CIntelligentCooling@PowerBattery@@QEAAHAEAHAEAW4ITSMode@12@@Z", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int GetITSMode(ref CIntelligentCooling instance, ref int itsVersion, ref ITSMode itsMode);

    [LibraryImport("PowerBattery.dll", EntryPoint = "?GetDispatcherVersion@CIntelligentCooling@PowerBattery@@QEAAHXZ", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int GetDispatcherVersion(ref CIntelligentCooling instance);

    [LibraryImport("PowerBattery.dll", EntryPoint = "?GetDispatcherMode@CIntelligentCooling@PowerBattery@@QEAAHAEAHAEAW4ITSMode@12@H@Z", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int GetDispatcherMode(ref CIntelligentCooling instance, ref int supportItsMode, ref ITSMode itsMode, int geekModeFlag);

    [LibraryImport("PowerBattery.dll", EntryPoint = "?SetDispatcherMode@CIntelligentCooling@PowerBattery@@QEAAHAEAW4ITSMode@12@H@Z", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int SetDispatcherMode(ref CIntelligentCooling instance, ref ITSMode itsMode, int var);

    [LibraryImport("PowerBattery.dll", EntryPoint = "?HasDispatcherDeviceNode@CIntelligentCooling@PowerBattery@@QEAAHXZ", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int HasDispatcherDeviceNode(ref CIntelligentCooling instance);
    #endregion

    public ITSMode LastItsMode { get; set; } = ITSMode.None;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return machineInfo.Properties.SupportsITSMode;
    }

    public async Task<ITSMode[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (mi.LegionSeries == LegionSeries.ThinkBook)
        {
            return Enum.GetValues(typeof(ITSMode))
                       .Cast<ITSMode>()
                       .Where(mode => mode != ITSMode.None)
                       .ToArray();
        }
        else
        {
            return Enum.GetValues(typeof(ITSMode))
                       .Cast<ITSMode>()
                       .Where(mode => mode != ITSMode.MmcGeek && mode != ITSMode.None)
                       .ToArray();
        }
    }

    public async Task<ITSMode> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await GetItsModeInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetStateAsync(ITSMode state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Log.Instance.Trace($"Setting ITS mode to: {state}");

        try
        {
            await SetItsModeInternalAsync(state, cancellationToken).ConfigureAwait(false);
            LastItsMode = state;

            Log.Instance.Trace($"ITS mode set successfully to: {state}");

            PublishNotification(state);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to set ITS mode to {state}", ex);

            throw;
        }
    }

    public void InvalidateResolution()
    {
    }

    private async Task<ITSMode> GetItsModeInternalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            CIntelligentCooling instance = default;
            var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var isThinkBook = machineInfo.LegionSeries == LegionSeries.ThinkBook;

            return HasDispatcherDeviceNode(ref instance) != 0 ? GetDispatcherModeInternal(ref instance, isThinkBook) : GetStandardModeInternal(ref instance);
        }
        catch (DllNotFoundException ex)
        {
            Log.Instance.TraceOnce(
                "feature-its-dll",
                "ITS mode native library missing; treating as unsupported.",
                ex);
            throw;
        }
    }

    private ITSMode GetDispatcherModeInternal(ref CIntelligentCooling instance, bool isThinkBook)
    {
        var supportFlag = 0;
        var mode = ITSMode.None;
        var errorCode = GetDispatcherMode(ref instance, ref supportFlag, ref mode, isThinkBook ? 1 : 0);

        Log.Instance.Trace($"GetDispatcherMode() executed. Error Code: {errorCode}");
        LogSupportedModes(supportFlag);
        ThrowIfNativeFailed(errorCode, nameof(GetDispatcherMode));

        return mode;
    }

    private ITSMode GetStandardModeInternal(ref CIntelligentCooling instance)
    {
        var version = 0;
        var mode = ITSMode.None;
        var errorCode = GetITSMode(ref instance, ref version, ref mode);

        Log.Instance.Trace($"GetITSMode() executed. Error Code: {errorCode}");
        Log.Instance.Trace($"ITS Version: {version}");
        ThrowIfNativeFailed(errorCode, nameof(GetITSMode));
        return mode;
    }

    private async Task SetItsModeInternalAsync(ITSMode state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CIntelligentCooling instance = default;
        var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        var isThinkBook = machineInfo.LegionSeries == LegionSeries.ThinkBook;
        var dispatcherVersion = GetDispatcherVersion(ref instance);

        int errorCode;
        var useDispatcher = dispatcherVersion >= DISPATCHER_VERSION_3;
        if (useDispatcher)
        {
            errorCode = SetDispatcherMode(ref instance, ref state, isThinkBook ? 1 : 0);
            Log.Instance.Trace($"Using SetDispatcherMode()");
            Log.Instance.Trace($"SetDispatcherMode executed. Error Code: {errorCode}");
        }
        else
        {
            errorCode = SetITSMode(ref instance, ref state);
            Log.Instance.Trace($"Using SetITSMode()");
            Log.Instance.Trace($"SetITSMode executed. Error Code: {errorCode}");
        }

        ThrowIfNativeFailed(errorCode, useDispatcher ? nameof(SetDispatcherMode) : nameof(SetITSMode));
        VerifyModeChange(ref instance, state, isThinkBook, useDispatcher);
    }

    private void VerifyModeChange(ref CIntelligentCooling instance, ITSMode expectedMode, bool isThinkBook, bool useDispatcher)
    {
        var currentMode = useDispatcher
            ? GetDispatcherModeInternal(ref instance, isThinkBook)
            : GetStandardModeInternal(ref instance);

        Log.Instance.Trace($"Mode verification - Expected: {expectedMode}, Actual: {currentMode}, Match: {expectedMode == currentMode}");

        if (currentMode != expectedMode)
            throw new InvalidOperationException(string.Format(Resource.Exception_FailedVerifyState, nameof(ITSModeFeature), expectedMode));
    }

    private static void ThrowIfNativeFailed(int errorCode, string operation)
    {
        if (errorCode == 0)
            return;

        throw new InvalidOperationException($"{operation} failed with status {errorCode}.");
    }

    private void LogSupportedModes(int supportFlag)
    {
        if (!Log.Instance.IsTraceEnabled)
        {
            return;
        }

        var modes = new[]
        {
            (1, ITSMode.ItsAuto),
            (2, ITSMode.MmcCool),
            (8, ITSMode.MmcPerformance),
            (16, ITSMode.MmcGeek)
        };

        foreach (var (flag, mode) in modes)
        {
            if ((supportFlag & flag) != 0)
            {
                Log.Instance.Trace($"Support ITSMode: {mode}");
            }
        }
    }

    private static void PublishNotification(ITSMode value)
    {
        switch (value)
        {
            case ITSMode.ItsAuto:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModeAuto, value.GetDisplayName()));
                break;
            case ITSMode.MmcCool:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModeCool, value.GetDisplayName()));
                break;
            case ITSMode.MmcPerformance:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModePerformance, value.GetDisplayName()));
                break;
            case ITSMode.MmcGeek:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ITSModeGeek, value.GetDisplayName()));
                break;
        }
    }
}
