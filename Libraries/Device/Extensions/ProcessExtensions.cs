using System;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class ProcessExtensions
{
    public static string? GetFileName(this Process process, int maxLength = 512)
    {
        var chars = new char[maxLength];
        unsafe
        {
            fixed (char* pChars = chars)
            {
                var handle = new HANDLE(process.SafeHandle.DangerousGetHandle());
                var length = PInvoke.K32GetModuleFileNameEx(handle, HMODULE.Null, pChars, (uint)maxLength);
                return length == 0 ? null : new string(chars, 0, (int)length);
            }
        }
    }
}
