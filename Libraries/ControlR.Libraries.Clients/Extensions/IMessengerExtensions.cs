namespace ControlR.Libraries.Clients.Extensions;

public static class MessengerExtensions
{
  public static IDisposable RegisterEventMessage(
    this IMessenger messenger,
    object recipient,
    EventMessageKind kind,
    Action handler)
  {
    return messenger.Register<ValueMessage<EventMessageKind>>(recipient, (_, message) =>
    {
      if (kind == message.Value)
      {
        handler();
      }

      return Task.CompletedTask;
    });
  }

  public static IDisposable RegisterEventMessage(
    this IMessenger messenger,
    object recipient,
    EventMessageKind kind,
    Func<Task> handler)
  {
    return messenger.Register<ValueMessage<EventMessageKind>>(recipient, async (_, message) =>
    {
      if (kind == message.Value)
      {
        await handler();
      }
    });
  }

  public static IDisposable RegisterEventMessage(
    this IMessenger messenger,
    object recipient,
    Action<object, EventMessageKind> handler)
  {
    return messenger.Register<ValueMessage<EventMessageKind>>(recipient, (subscriber, message) =>
    {
      handler(subscriber, message.Value);
      return Task.CompletedTask;
    });
  }

  public static IDisposable RegisterEventMessage(
    this IMessenger messenger,
    object recipient,
    Func<object, EventMessageKind, Task> handler)
  {
    return messenger.Register<ValueMessage<EventMessageKind>>(recipient,
      async (subscriber, message) => { await handler(subscriber, message.Value); });
  }

  public static Task SendGenericMessage(this IMessenger messenger, EventMessageKind kind)
  {
    return messenger.Send(new ValueMessage<EventMessageKind>(kind));
  }
}