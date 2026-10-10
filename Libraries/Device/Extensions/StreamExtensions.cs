using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib.Extensions;

// ReSharper disable LocalizableElement

public static class StreamExtensions
{
    public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!source.CanRead)
            throw new ArgumentException(Resource.Exception_HasToBeReadable, nameof(source));
        if (!destination.CanWrite)
            throw new ArgumentException(Resource.Exception_HasToBeWritable, nameof(destination));

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }
}
