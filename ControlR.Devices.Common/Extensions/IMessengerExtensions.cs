using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Messages;
using ControlR.Viewer.Models.Messages;

namespace ControlR.Devices.Common.Extensions;

internal static class IMessengerExtensions
{
    public static void RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        GenericMessageKind kind,
        Action handler)
    {
        messenger.Register<GenericMessage<GenericMessageKind>>(recipient, (message) =>
        {
            if (kind == message.Value)
            {
                handler();
            }

            return Task.CompletedTask;
        });
    }

    public static void RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        GenericMessageKind kind,
        Func<Task> handler)
    {
        messenger.Register<GenericMessage<GenericMessageKind>>(recipient, async (message) =>
        {
            if (kind == message.Value)
            {
                await handler();
            }
        });
    }

    public static void RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        Action<GenericMessageKind> handler)
    {
        messenger.Register<GenericMessage<GenericMessageKind>>(recipient, (message) =>
        {
            handler(message.Value);
            return Task.CompletedTask;
        });
    }

    public static void RegisterGenericMessage(
        this IMessenger messenger,
        object recipient,
        Func<GenericMessageKind, Task> handler)
    {
        messenger.Register<GenericMessage<GenericMessageKind>>(recipient, async (message) =>
        {
            await handler(message.Value);
        });
    }

    public static void SendGenericMessage(this IMessenger messenger, GenericMessageKind kind)
    {
        messenger.Send(new GenericMessage<GenericMessageKind>(kind));
    }
}