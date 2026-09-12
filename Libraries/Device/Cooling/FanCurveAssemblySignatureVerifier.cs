using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Verifies the Authenticode signature of the legacy UniversalFanControl extension
/// before it is loaded into the elevated application process.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class FanCurveAssemblySignatureVerifier
{
    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint WtdDisableMd2Md4 = 0x2000;
    private const uint WtdRevocationCheckNone = 0x10;

    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static bool TryVerifyFile(string filePath, out int trustStatus)
    {
        trustStatus = unchecked((int)0x800B0100); // TRUST_E_NOSIGNATURE

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        var fileInfo = new WintrustFileInfo
        {
            Size = (uint)Marshal.SizeOf<WintrustFileInfo>(),
            FilePath = filePath
        };

        var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var data = new WintrustData
            {
                Size = (uint)Marshal.SizeOf<WintrustData>(),
                UiChoice = WtdUiNone,
                RevocationChecks = WtdRevokeNone,
                UnionChoice = WtdChoiceFile,
                File = fileInfoPtr,
                StateAction = WtdStateActionVerify,
                ProviderFlags = WtdDisableMd2Md4 | WtdRevocationCheckNone
            };

            var action = GenericVerifyV2;
            trustStatus = WinVerifyTrust(IntPtr.Zero, ref action, ref data);

            data.StateAction = WtdStateActionClose;
            _ = WinVerifyTrust(IntPtr.Zero, ref action, ref data);

            return trustStatus == 0;
        }
        finally
        {
            Marshal.DestroyStructure<WintrustFileInfo>(fileInfoPtr);
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, ref WintrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
