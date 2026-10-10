using Autofac;
using Autofac.Builder;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class ContainerBuilderExtensions
{
    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> Register<T>(this ContainerBuilder cb, bool selfOnly = false) where T : notnull
    {
        var registration = cb.RegisterType<T>().AsSelf();
        if (!selfOnly)
            registration = registration.AsImplementedInterfaces();
        return registration.SingleInstance();
    }

    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterTransient<T>(
        this ContainerBuilder builder, bool selfOnly = false) where T : notnull
    {
        var registration = builder.RegisterType<T>().AsSelf();
        if (!selfOnly)
            registration = registration.AsImplementedInterfaces();
        // No SingleInstance() — each Resolve creates a new instance
        return registration;
    }
}
