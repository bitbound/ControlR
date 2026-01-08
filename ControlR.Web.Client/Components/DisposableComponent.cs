namespace ControlR.Web.Client.Components;

public class DisposableComponent : ComponentBase, IDisposable, IAsyncDisposable
{
  private bool _disposedValue;

  protected DisposableCollection Disposables { get; } = [];

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  public async ValueTask DisposeAsync()
  {
    // Do not change this code. Put cleanup code in 'DisposeAsync(bool disposing)' method
    await DisposeAsync(disposing: true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        Disposables.Dispose();
      }

      _disposedValue = true;
    }
  }

  protected virtual ValueTask DisposeAsync(bool disposing)
  {
    Dispose(disposing);
    return ValueTask.CompletedTask;
  }

  protected void RegisterDisposable(IDisposable disposable)
  {
    Disposables.Add(disposable);
  }
}