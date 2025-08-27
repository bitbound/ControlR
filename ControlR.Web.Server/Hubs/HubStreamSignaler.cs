namespace ControlR.Web.Server.Hubs;

public interface IHubStreamSignaler : IDisposable
{
  Guid StreamId { get; }
  ManualResetEventAsync EndSignal { get; }
  ManualResetEventAsync ReadySignal { get; }
  string RequesterConnectionId { get; }
  string ResponderConnectionId { get; }
  Type ItemType { get; }
  object? Metadata { get; set; }
}

public class HubStreamSignaler<T>(Guid streamId, Action? onDispose = null) : IHubStreamSignaler
{
  private readonly Action? _onDispose = onDispose;
  private bool _disposedValue;

  public ManualResetEventAsync EndSignal { get; } = new();
  public ManualResetEventAsync ReadySignal { get; } = new();
  public string RequesterConnectionId { get; internal set; } = string.Empty;
  public string ResponderConnectionId { get; internal set; } = string.Empty;
  public IAsyncEnumerable<T>? Stream { get; internal set; }
  public Guid StreamId { get; } = streamId;
  public Type ItemType => typeof(T);
  public object? Metadata { get; set; }

  public void SetStream(IAsyncEnumerable<T> stream, string responderConnectionId)
  {
    ResponderConnectionId = responderConnectionId;
    Stream = stream;
    ReadySignal.Set();
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_disposedValue) return;
    if (disposing)
    {
      EndSignal.Dispose();
      ReadySignal.Dispose();
      _onDispose?.Invoke();
    }
    _disposedValue = true;
  }
}