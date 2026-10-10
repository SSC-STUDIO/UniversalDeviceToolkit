namespace UniversalDeviceToolkit.Lib.Messaging.Messages;

public readonly struct NotificationMessage : IMessage
{
    public NotificationMessage(NotificationType type, params object[] args) : this(type, NotificationPriority.Normal, args) { }

    public NotificationMessage(NotificationType type, NotificationPriority priority, params object[] args)
    {
        Type = type;
        Priority = priority;
        Args = args;
    }

    public NotificationType Type { get; }

    public NotificationPriority Priority { get; }

    public object[] Args { get; }

    public override string ToString() => $@"{nameof(Type)}: {Type}, {nameof(Priority)}: {Priority}, {nameof(Args)}: [{string.Join(", ", Args)}]";
}
