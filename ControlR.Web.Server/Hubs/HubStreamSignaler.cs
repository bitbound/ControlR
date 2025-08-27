namespace ControlR.Web.Server.Hubs;

public class HubStreamSignaler(Guid streamId, Action? onDispose = null) : IDisposable
{
  private readonly Action? _onDispose = onDispose;
  private bool _disposedValue;

  public ManualResetEventAsync EndSignal { get; } = new();
  public ManualResetEventAsync ReadySignal { get; } = new();
  public string RequesterConnectionId { get; internal set; } = string.Empty;
  public string ResponderConnectionId { get; internal set; } = string.Empty;
  public IAsyncEnumerable<byte[]>? Stream { get; internal set; }
  public Guid StreamId { get; init; } = streamId;

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  public void SetStream(IAsyncEnumerable<byte[]> stream, string responderConnectionId)
  {
    ResponderConnectionId = responderConnectionId;
    Stream = stream;
    ReadySignal.Set();
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        EndSignal.Dispose();
        ReadySignal.Dispose();
        _onDispose?.Invoke();
      }
      _disposedValue = true;
    }
  }
}