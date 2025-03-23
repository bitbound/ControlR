namespace ControlR.Web.Server.Helpers;

public class StreamSignaler(Guid streamId) : IDisposable
{
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

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        EndSignal.Dispose();
        ReadySignal.Dispose();
      }
      _disposedValue = true;
    }
  }
}