using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UniversalDeviceToolkit.Shared.Logging;

namespace UniversalDeviceToolkit.Shared.Messaging;

/// <summary>
/// Cross-platform publish/subscribe messaging center.
/// Extracted from Lib.Messaging.MessagingCenter — replaces Serilog Log with SharedLog,
/// ConditionalWeakTable + lock-snapshot implementation preserved verbatim.
/// </summary>
public static class MessagingCenter
{
    public static void Publish<T>(T data) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            Messenger.Instance.Publish(data);
        }
        catch (Exception ex)
        {
            SharedLog.Warning($"MessagingCenter.Publish<{typeof(T).Name}> failed: {ex.Message}", ex);
        }
    }

    public static void Subscribe<T>(object subscriber, Action<T> handler) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(handler);

        Messenger.Instance.Subscribe<T>(subscriber, msg =>
        {
            try
            {
                handler(msg);
            }
            catch (Exception ex)
            {
                SharedLog.Warning($"MessagingCenter handler for {typeof(T).Name} failed: {ex.Message}", ex);
            }
        });
    }

    public static void Subscribe<T>(object subscriber, Action handler) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(handler);

        Messenger.Instance.Subscribe<T>(subscriber, _ =>
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                SharedLog.Warning($"MessagingCenter handler for {typeof(T).Name} failed: {ex.Message}", ex);
            }
        });
    }

    public static void Unsubscribe<T>(object subscriber) where T : IMessage
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        Messenger.Instance.Unsubscribe<T>(subscriber);
    }

    public static void Unsubscribe(object subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        Messenger.Instance.UnsubscribeAll(subscriber);
    }
}

internal sealed class Messenger
{
    public static readonly Messenger Instance = new();

    private readonly object _lock = new();
    private readonly ConditionalWeakTable<object, Dictionary<Type, List<Delegate>>> _subs
        = new();

    public void Publish<T>(T data)
    {
        List<Delegate> snapshot;
        lock (_lock)
        {
            snapshot = CollectHandlers<T>();
        }
        foreach (var handler in snapshot)
        {
            ((Action<T>)handler)(data);
        }
    }

    public void Subscribe<T>(object subscriber, Action<T> handler)
    {
        lock (_lock)
        {
            var typeKey = typeof(T);
            if (!_subs.TryGetValue(subscriber, out var byType))
            {
                byType = new Dictionary<Type, List<Delegate>>();
                _subs.Add(subscriber, byType);
            }
            if (!byType.TryGetValue(typeKey, out var list))
            {
                list = new List<Delegate>();
                byType[typeKey] = list;
            }
            list.Add(handler);
        }
    }

    public void Unsubscribe<T>(object subscriber)
    {
        lock (_lock)
        {
            if (_subs.TryGetValue(subscriber, out var byType))
            {
                byType.Remove(typeof(T));
                if (byType.Count == 0)
                {
                    // CWT 无法主动删除 key，依赖 GC 自动回收
                    byType.Clear();
                }
            }
        }
    }

    public void UnsubscribeAll(object subscriber)
    {
        lock (_lock)
        {
            if (_subs.TryGetValue(subscriber, out var byType))
            {
                // CWT 无法主动删除 key，依赖 GC 自动回收
                byType.Clear();
            }
        }
    }

    private List<Delegate> CollectHandlers<T>()
    {
        var typeKey = typeof(T);
        var result = new List<Delegate>();
        foreach (var kvp in _subs)
        {
            if (kvp.Value.TryGetValue(typeKey, out var list))
                result.AddRange(list);
        }
        return result;
    }
}
