using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.CLI.Lib.Extensions;

public static class PipeStreamExtensions
{
    private static readonly Encoding Encoding = Encoding.UTF8;

    private static readonly JsonSerializerOptions PipeJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static async Task WriteObjectAsync<T>(this PipeStream stream, T obj, CancellationToken token = default)
    {
        if (stream.ReadMode != PipeTransmissionMode.Message)
            throw new InvalidOperationException("ReadMode is not PipeTransmissionMode.Message");

        var str = JsonSerializer.Serialize(obj, PipeJsonOptions);
        var bytes = Encoding.GetBytes(str);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private const int MaxBufferSize = 16 * 1024 * 1024; // 16 MB

    public static async Task<T?> ReadObjectAsync<T>(this PipeStream stream, CancellationToken token = default)
    {
        if (stream.ReadMode != PipeTransmissionMode.Message)
            throw new InvalidOperationException("ReadMode is not PipeTransmissionMode.Message");

        var buffer = new byte[1024];
        var builder = new StringBuilder();
        var totalBytesRead = 0;

        try
        {
            do
            {
                var bytesRead = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (bytesRead == 0)
                    throw new IOException("Pipe stream was closed unexpectedly.");

                totalBytesRead += bytesRead;
                if (totalBytesRead > MaxBufferSize)
                    throw new IOException($"Pipe message exceeded maximum buffer size of {MaxBufferSize} bytes.");

                builder.Append(Encoding.GetString(buffer, 0, bytesRead));
            } while (!stream.IsMessageComplete);
        }
        catch (IOException) when (!stream.IsConnected)
        {
            throw new IOException("Pipe connection was broken while reading.");
        }

        return JsonSerializer.Deserialize<T>(builder.ToString(), PipeJsonOptions);
    }
}
