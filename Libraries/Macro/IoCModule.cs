using Autofac;
#if WINDOWS
using UniversalDeviceToolkit.Abstractions.Macro;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Macro.Utils;
#endif

namespace UniversalDeviceToolkit.Lib.Macro;

public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
#if WINDOWS
        builder.Register<MacroSettings>();
        builder.Register<MacroController>().AsSelf().As<IMacroController>();
#endif
    }
}
