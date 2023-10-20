using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using ControlR.Viewer.Models.Messages;

namespace ControlR.Viewer.Extensions;

internal static class IMessengerExtensions
{
    public static void SendParameterlessMessage(this IMessenger messenger, ParameterlessMessageKind kind)
    {
        messenger.Send(new ValueChangedMessage<ParameterlessMessageKind>(kind));
    }

    public static void RegisterParameterless(
        this IMessenger messenger,
        object recipient,
        ParameterlessMessageKind kind,
        Action handler)
    {
        messenger.Register<ValueChangedMessage<ParameterlessMessageKind>>(recipient, (r, m) =>
        {
            if (kind == m.Value)
            {
                handler();
            }
        });
    }

    public static void RegisterParameterless(
        this IMessenger messenger,
        object recipient,
        ParameterlessMessageKind kind,
        Func<Task> handler)
{
        messenger.Register<ValueChangedMessage<ParameterlessMessageKind>>(recipient, async (r, m) =>
        {
            if (kind == m.Value)
            {
                await handler();
            }
        });
    }

    public static void RegisterParameterless(
        this IMessenger messenger,
        object recipient,
        Action<ParameterlessMessageKind> handler)
    {
        messenger.Register<ValueChangedMessage<ParameterlessMessageKind>>(recipient, (r, m) =>
        {
            handler(m.Value);
        });
    }

    public static void RegisterParameterless(
        this IMessenger messenger,
        object recipient,
        Func<ParameterlessMessageKind, Task> handler)
    {
        messenger.Register<ValueChangedMessage<ParameterlessMessageKind>>(recipient, async (r, m) =>
        {
            await handler(m.Value);
        });
    }
}
