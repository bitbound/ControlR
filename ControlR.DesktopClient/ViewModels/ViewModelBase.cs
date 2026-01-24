using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Shared.Collections;

namespace ControlR.DesktopClient.ViewModels;

public abstract class ViewModelBase<TView> : ObservableObject, IViewModelBase
{
  private readonly SemaphoreSlim _initializeLock = new(1, 1);
  private bool _disposedValue;
  // Prevent derived types from accidentally being initialized more than once
  // by providing a single entry point that callers should invoke. Derived
  // view models should override `OnInitializeAsync` instead of `Initialize`.
  private bool _initialized;

  public Type ViewType { get; } = typeof(TView);

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
  public async Task Initialize()
  {
    using var lockScope = await _initializeLock.AcquireLockAsync(CancellationToken.None);

    if (_initialized)
    {
      return;
    }

    await OnInitializeAsync().ConfigureAwait(false);
    _initialized = true;
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        Disposables.Dispose();
        _initializeLock.Dispose();
      }

      _disposedValue = true;
    }
  }

  protected virtual ValueTask DisposeAsync(bool disposing)
  {
    Dispose(disposing);
    return ValueTask.CompletedTask;
  }

  protected virtual Task OnInitializeAsync()
  {
    return Task.CompletedTask;
  }
}
