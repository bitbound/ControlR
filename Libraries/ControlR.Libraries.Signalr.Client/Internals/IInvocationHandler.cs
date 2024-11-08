using System.Reflection;
using System.Threading.Channels;

namespace ControlR.Libraries.Signalr.Client.Internals;

/// <summary>
/// Represents an object that can handle invocations of methods and
/// proxy them to a SignalR hub.
/// </summary>
public interface IInvocationHandler
{
  Task<T> InvokeAsync<T>(MethodInfo method, object[] args);

  ChannelReader<T> InvokeChannel<T>(MethodInfo method, object[] args);

  ValueTask<T> InvokeValueTaskAsync<T>(MethodInfo method, object[] args);

  Task InvokeVoidAsync(MethodInfo method, object[] args);
  IAsyncEnumerable<T> Stream<T>(MethodInfo method, object[] args);
}
