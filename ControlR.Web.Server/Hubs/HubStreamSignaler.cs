using System.Threading.Channels;

namespace ControlR.Web.Server.Hubs;

public sealed class HubStreamSignaler<T>(Guid streamId, Action? onDispose = null) : IDisposable
{
  private readonly Channel<T> _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(10)
  {
    SingleReader = true,
    SingleWriter = true,
    FullMode = BoundedChannelFullMode.Wait
  });
  private readonly Action? _onDispose = onDispose;
  private readonly CancellationTokenSource _streamCancelledSource = new();
  private readonly CancellationTokenSource _writeCompletedSource = new();

  private int _disposedValue; 

  public object? Metadata { get; set; }
  public ChannelReader<T> Reader => _channel.Reader;
  public Guid StreamId { get; } = streamId;
  public CancellationToken WriteCompleted => _writeCompletedSource.Token;
  public ChannelWriter<T> Writer => _channel.Writer;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  public void SetWriteCompleted(Exception? exception = null)
  {
     _channel.Writer.TryComplete(exception);
    _writeCompletedSource.Cancel();
  }

  public async Task WriteFromChannelReader(ChannelReader<T> reader, CancellationToken cancellationToken)
  {
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _streamCancelledSource.Token);
    try
    {
      await foreach (var item in reader.ReadAllAsync(linkedCts.Token))
      {
        await _channel.Writer.WriteAsync(item, linkedCts.Token);
      }
      _channel.Writer.TryComplete();
    }
    catch (Exception ex)
    {
      _channel.Writer.TryComplete(ex);
      throw;
    }
    finally
    {
      await _writeCompletedSource.CancelAsync();
    }
  }


  private void Dispose(bool disposing)
  {

    if (Interlocked.CompareExchange(ref _disposedValue, 1, 0) != 0)
    {
      return;
    }

    if (!disposing) return;
    _streamCancelledSource.Cancel();
    _streamCancelledSource.Dispose();
    _channel.Writer.TryComplete();
    _onDispose?.Invoke();
  }
}