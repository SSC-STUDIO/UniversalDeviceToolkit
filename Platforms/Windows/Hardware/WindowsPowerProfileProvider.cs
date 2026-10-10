using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.Windows.Hardware;

// TODO: 后续阶段将委托给 Lib 中的 Windows Power Plan API 实现
public sealed class WindowsPowerProfileProvider : IPowerProfileProvider
{
    public bool IsAvailable => false;
    public IReadOnlyList<string> GetAvailableProfiles() => [];
    public string? GetActiveProfile() => null;
    public Task SetActiveProfileAsync(string profileName) => Task.CompletedTask;
}
