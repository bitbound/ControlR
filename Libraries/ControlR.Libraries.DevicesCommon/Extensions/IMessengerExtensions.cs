using Bitbound.SimpleMessenger;
using ControlR.Libraries.DevicesCommon.Messages;

namespace ControlR.Libraries.DevicesCommon.Extensions;

internal static class IMessengerExtensions
{
    public static IDisposable RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        GenericMessageKind kind,
        Action handler)
    {
        return messenger.Register<GenericMessage<GenericMessageKind>>(recipient, (subscriber, message) =>
        {
            if (kind == message.Value)
            {
                handler();
            }

            return Task.CompletedTask;
        });
    }

    public static IDisposable RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        GenericMessageKind kind,
        Func<Task> handler)
    {
        return messenger.Register<GenericMessage<GenericMessageKind>>(recipient, async (subscriber, message) =>
        {
            if (kind == message.Value)
            {
                await handler();
            }
        });
    }

    public static IDisposable RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        Action<object, GenericMessageKind> handler)
    {
        return messenger.Register<GenericMessage<GenericMessageKind>>(recipient, (subscriber, message) =>
        {
            handler(subscriber, message.Value);
            return Task.CompletedTask;
        });
    }

    public static IDisposable RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        Func<object, GenericMessageKind, Task> handler)
    {
        return messenger.Register<GenericMessage<GenericMessageKind>>(recipient, async (subscriber, message) =>
        {
            await handler(subscriber, message.Value);
        });
    }

    public static Task SendGenericMessage(this IMessenger messenger, GenericMessageKind kind)
    {
        return messenger.Send(new GenericMessage<GenericMessageKind>(kind));
    }
}