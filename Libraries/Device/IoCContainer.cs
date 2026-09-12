using System;
using System.Threading;
using Autofac;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib;

public static class IoCContainer
{
    private static readonly object Lock = new();

    private static IContainer? _container;

    public static void Initialize(params Module[] modules)
        => Initialize(null, modules);

    public static void Initialize(Action<ContainerBuilder>? preBuild, params Module[] modules)
    {
        lock (Lock)
        {
            if (_container is not null)
                throw ExceptionHelper.IoCAlreadyInitialized();

            var cb = new ContainerBuilder();

            foreach (var module in modules)
                cb.RegisterModule(module);

            preBuild?.Invoke(cb);

            _container = cb.Build();
        }
    }

    public static T Resolve<T>() where T : notnull
    {
        lock (Lock)
        {
            if (_container is null)
                throw ExceptionHelper.IoCMustBeInitialized(nameof(T));
            return _container.Resolve<T>();
        }
    }

    public static void Dispose()
    {
        lock (Lock)
        {
            _container?.Dispose();
            _container = null;
        }
    }

    public static T? TryResolve<T>() where T : class
    {
        // Do not make UI capability probes wait on the container build lock;
        // they can retry after initialization.
        if (Volatile.Read(ref _container) is null)
            return null;

        lock (Lock)
        {
            if (_container is null)
                return null;

            _ = _container.TryResolve(out T? value);
            return value;
        }
    }

}
