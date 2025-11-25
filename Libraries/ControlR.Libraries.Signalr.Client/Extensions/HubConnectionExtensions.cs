using System.Threading.Channels;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Signalr.Client.Extensions;

public static class HubConnectionExtensions
{
  public static async Task StreamBytes<THub>(
    this IHubConnection<THub> hubConnection,
    string hubMethodName,
    object[] args,
    byte[] data,
    int channelCapacity,
    CancellationToken cancellationToken)
    where THub : class
  {
    var channel = Channel.CreateBounded<byte[]>(channelCapacity);

    var writeTask = Task.Run(async () =>
    {
      try
      {
        foreach (var chunk in data.Chunk(AppConstants.SignalrMaxMessageSize))
        {
          await channel.Writer.WriteAsync(chunk, cancellationToken);
        }
        channel.Writer.TryComplete();
      }
      catch (Exception ex)
      {
        channel.Writer.TryComplete(ex);
        throw;
      }
    }, cancellationToken);

    var argsWithChannel = args.Append(channel.Reader).ToArray();
    await hubConnection.Send(hubMethodName, argsWithChannel, cancellationToken);
    await writeTask;
  }

  public static async Task StreamData<THub, TData>(
    this IHubConnection<THub> hubConnection,
    string hubMethodName,
    object[] args,
    IEnumerable<TData> data,
    int chunkSize,
    int channelCapacity,
    CancellationToken cancellationToken)
    where THub : class
  {
    var channel = Channel.CreateBounded<TData[]>(channelCapacity);

    var writeTask = Task.Run(async () =>
    {
      try
      {
        foreach (var chunk in data.Chunk(chunkSize))
        {
          await channel.Writer.WriteAsync(chunk, cancellationToken);
        }
        channel.Writer.TryComplete();
      }
      catch (Exception ex)
      {
        channel.Writer.TryComplete(ex);
        throw;
      }
    }, cancellationToken);

    var argsWithChannel = args.Append(channel.Reader).ToArray();
    await hubConnection.Send(hubMethodName, argsWithChannel, cancellationToken);
    await writeTask;
  }
}