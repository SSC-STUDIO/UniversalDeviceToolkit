namespace UniversalDeviceToolkit.Lib.Messaging.Messages;

public enum MainWindowVisibilityAction
{
    Show,
    Hide
}

public readonly struct MainWindowVisibilityMessage(MainWindowVisibilityAction action) : IMessage
{
    public MainWindowVisibilityAction Action { get; } = action;
}
