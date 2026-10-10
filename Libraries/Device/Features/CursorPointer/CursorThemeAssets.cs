using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UniversalDeviceToolkit.Lib.Features.CursorPointer;

/// <summary>Persists cursor files outside replaceable build and installation directories.</summary>
internal static class CursorThemeAssets
{
    internal static (string BasePath, string AnimationPath) Persist(
        string basePath, string animationPath, string destination, IEnumerable<string> fileNames)
    {
        // Read every required asset before changing any cached file or registry value.
        var files = fileNames.Select(name => (Name: name, Bytes: File.ReadAllBytes(Path.Combine(
            name.EndsWith(".ani", StringComparison.OrdinalIgnoreCase) ? animationPath : basePath, name)))).ToArray();
        Directory.CreateDirectory(destination);
        foreach (var (name, bytes) in files)
        {
            var target = Path.Combine(destination, name);
            if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes)) continue;
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, target, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        return (destination, destination);
    }
}
