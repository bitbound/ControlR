namespace ControlR.Libraries.Messenger.Extensions;

public static class MessengerExtensions
{
  public static IDisposable RegisterEvent(
    this IMessenger messenger,
    object recipient,
    Guid eventKind,
    Action handler)
  {
    return messenger.Register<EventMessage>(recipient, (_, message) =>
    {
      if (eventKind == message.EventKind)
      {
        handler();
      }

      return Task.CompletedTask;
    });
  }

  public static IDisposable RegisterEvent(
    this IMessenger messenger,
    object recipient,
    Guid eventKind,
    Func<Task> handler)
  {
    return messenger.Register<EventMessage>(recipient, async (_, message) =>
    {
      if (eventKind == message.EventKind)
      {
        await handler();
      }
    });
  }

  public static IDisposable RegisterEvent(
    this IMessenger messenger,
    object recipient,
    Action<object, Guid> handler)
  {
    return messenger.Register<EventMessage>(recipient, (subscriber, message) =>
    {
      handler(subscriber, message.EventKind);
      return Task.CompletedTask;
    });
  }

  public static IDisposable RegisterEvent(
    this IMessenger messenger,
    object recipient,
    Func<object, Guid, Task> handler)
  {
    return messenger.Register<EventMessage>(recipient,
      async (subscriber, message) => { await handler(subscriber, message.EventKind); });
  }

  public static Task SendEvent(this IMessenger messenger, Guid eventKind)
  {
    return messenger.Send(new EventMessage(eventKind));
  }
  
}