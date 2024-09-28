using System.Reflection;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Libraries.Signalr.Client.Internals;

/// <summary>
/// An invocation handler that proxies method invocations through the
/// underlying SignalR <see cref="HubConnection"/>.
/// </summary>
/// <typeparam name="THub">
///   The public interface of the hub.
/// </typeparam>
/// <typeparam name="TClient">
///   The public interface of the client.
/// </typeparam>
/// <param name="hubConnection"></param>
internal sealed class ProxyInvocationHandler<THub, TClient>(HubConnection<THub, TClient> hubConnection) : IInvocationHandler
  where THub : class
  where TClient : class
{
  public async Task<T> InvokeAsync<T>(MethodInfo method, object[] args)
  {
    return await hubConnection.Connection.InvokeCoreAsync<T>(method.Name, args);
  }

  public ChannelReader<T> InvokeChannel<T>(MethodInfo method, object[] args)
  {
    // This isn't ideal and shouldn't be used on servers. I couldn't
    // get the channel to bind when wrapped in a Task. However, this
    // library will be used on clients, and he ProxyGenerator ensures
    // that the hub method is also synchronous.  So the invoke should
    // return the reader immediately and not tie up a thread for long.
    if (args.Length > 0 && args[^1] is CancellationToken cancellationToken)
    {
      return RunSync(() =>
        hubConnection.Connection.StreamAsChannelCoreAsync<T>(method.Name, args[..^1], cancellationToken));
    }
    return RunSync(() => hubConnection.Connection.StreamAsChannelCoreAsync<T>(method.Name, args));
  }

  public async ValueTask<T> InvokeValueTaskAsync<T>(MethodInfo method, object[] args)
  {
    return await hubConnection.Connection.InvokeCoreAsync<T>(method.Name, args);
  }

  public async Task InvokeVoidAsync(MethodInfo method, object[] args)
  {
    await hubConnection.Connection.InvokeCoreAsync(method.Name, args);
  }
  public IAsyncEnumerable<T> Stream<T>(MethodInfo method, object[] args)
  {
    if (args.Length > 0 && args[^1] is CancellationToken cancellationToken)
    {
      return hubConnection.Connection.StreamAsyncCore<T>(method.Name, args[..^1], cancellationToken);
    }
    return hubConnection.Connection.StreamAsyncCore<T>(method.Name, args);
  }
  /// <summary>
  /// Wrap the invoke in another Task to prevent deadlocks.
  /// </summary>
  private static T RunSync<T>(Func<Task<T>> func)
  {
    return Task.Run(() => func()).GetAwaiter().GetResult();
  }
}
