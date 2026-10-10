using System;
using System.Security.Cryptography;
using System.Text;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Random session token helpers for NetworkProxy named-pipe IPC.</summary>
public static class NetworkProxySessionToken
{
    public const int MinimumLength = 16;

    /// <summary>
    /// Environment variable used to pass the session token to the worker process.
    /// Prefer this over <c>--token</c> so the secret does not appear on the process command line.
    /// </summary>
    public const string WorkerTokenEnvironmentVariable = "UDT_NETWORK_PROXY_TOKEN";

    public static string Create(int byteLength = 24)
    {
        if (byteLength < 12)
            byteLength = 12;

        Span<byte> bytes = stackalloc byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool IsValidFormat(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var trimmed = token.Trim();
        if (trimmed.Length < MinimumLength)
            return false;

        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '+' or '/' or '=')
                continue;
            return false;
        }

        return true;
    }

    public static bool Matches(string? provided, string? expected)
    {
        if (!IsValidFormat(provided) || !IsValidFormat(expected))
            return false;

        var a = Encoding.UTF8.GetBytes(provided!.Trim());
        var b = Encoding.UTF8.GetBytes(expected!.Trim());
        if (a.Length != b.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
