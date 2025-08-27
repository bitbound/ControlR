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
  /// <summary>
  /// Sends a hub method that does not expect a return value but may include
  /// client-to-server streaming parameters such as IAsyncEnumerable{T}.
  /// Uses SendCoreAsync under the hood so the presence of streaming params
  /// is respected by SignalR.
  /// </summary>
  Task SendAsync(MethodInfo method, object[] args);
  IAsyncEnumerable<T> Stream<T>(MethodInfo method, object[] args);
}
