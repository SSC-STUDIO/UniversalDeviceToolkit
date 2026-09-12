namespace UniversalDeviceToolkit.Lib.Messaging;

using global::System;

/// <summary>Compatibility facade over the cross-platform messaging center.</summary>
public static class MessagingCenter
{
    public static void Publish<T>(T data) where T : Messages.IMessage =>
        UniversalDeviceToolkit.Shared.Messaging.MessagingCenter.Publish(data);

    public static void Subscribe<T>(object subscriber, Action<T> handler) where T : Messages.IMessage =>
        UniversalDeviceToolkit.Shared.Messaging.MessagingCenter.Subscribe(subscriber, handler);

    public static void Subscribe<T>(object subscriber, Action handler) where T : Messages.IMessage =>
        UniversalDeviceToolkit.Shared.Messaging.MessagingCenter.Subscribe<T>(subscriber, handler);

    public static void Unsubscribe<T>(object subscriber) where T : Messages.IMessage =>
        UniversalDeviceToolkit.Shared.Messaging.MessagingCenter.Unsubscribe<T>(subscriber);

    public static void Unsubscribe(object subscriber) =>
        UniversalDeviceToolkit.Shared.Messaging.MessagingCenter.Unsubscribe(subscriber);
}
