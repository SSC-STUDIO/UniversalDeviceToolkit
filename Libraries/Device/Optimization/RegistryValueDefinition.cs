using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Optimization;

public record struct RegistryValueDefinition(string Hive, string SubKey, string ValueName, object Value, RegistryValueKind Kind);
