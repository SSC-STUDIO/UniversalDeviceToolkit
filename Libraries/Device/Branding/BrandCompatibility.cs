// NOTE: Do not use UniversalDeviceToolkit.Lib.Compatibility as a namespace —
// Compatibility is already a static class in UniversalDeviceToolkit.Lib and a
// child namespace would shadow it (CS0234 on Compatibility.GetMachineInformationAsync).
namespace UniversalDeviceToolkit.Lib.Branding;

/// <summary>
/// Brand and assembly-name constants after the UniversalDeviceToolkit hard cutover.
/// </summary>
/// <remarks>
/// <para>
/// User-facing product names use Universal Device Toolkit (UDT) branding.
/// Primary assembly simple name matches project <c>AssemblyName</c>
/// (<c>UniversalDeviceToolkit.Lib</c>).
/// </para>
/// <para>
/// <b>Legacy assembly names are not a load-time bridge.</b> Legacy constants remain
/// for migration messaging and AppData folder detection only.
/// </para>
/// <para>
/// See <c>Docs/NamespaceMigration.md</c> for dual IPC pipe names and automation
/// env-var <c>UDT_*</c> (primary) / <c>LLT_*</c> (alias) dual-write.
/// </para>
/// <para>
/// Display-name values align with <see cref="UniversalDeviceToolkit.Lib.Utils.AppIdentity"/>.
/// </para>
/// </remarks>
public static class BrandCompatibility
{
    /// <summary>User-facing product display name (WPF process / installer brand).</summary>
    public const string ProductDisplayName = "Universal Device Toolkit";

    /// <summary>Compact product identifier without spaces (AppData folder, compact brand).</summary>
    public const string ProductCompactName = "UniversalDeviceToolkit";

    /// <summary>Legacy product display name retained for migration messaging and docs.</summary>
    public const string LegacyProductDisplayName = "Lenovo Legion Toolkit";

    /// <summary>
    /// Legacy compact product identifier (legacy AppData folder / historical brand token).
    /// </summary>
    public const string LegacyProductCompactName = "LenovoLegionToolkit";

    /// <summary>
    /// Primary core library assembly simple name (no extension).
    /// Matches <c>UniversalDeviceToolkit.Lib</c> project <c>AssemblyName</c> after hard cutover.
    /// </summary>
    public const string PreferredAssemblyLib = "UniversalDeviceToolkit.Lib";

    /// <summary>
    /// Legacy core library assembly simple name — messaging / detection only (not a runtime bind target).
    /// </summary>
    public const string LegacyAssemblyLib = "LenovoLegionToolkit.Lib";
}
