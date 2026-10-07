using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal sealed class ConsoleInstallProgress(TextWriter output) : IProgress<object>
{
    public void Report(object value)
    {
        var progress = JsonSerializer.SerializeToElement(value);
        if (progress.TryGetProperty("phase", out var phase) && phase.GetString() == "warning"
            && progress.TryGetProperty("message", out var message))
        {
            output.WriteLine(message.GetString());
            output.Flush();
        }
    }
}
