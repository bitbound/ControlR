using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Shared.Collections;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IViewModelBase : IDisposable, IAsyncDisposable
{
  /// <summary>
  /// The name of the view associated with this view model.
  /// </summary>
  Type ViewType { get; }

  /// <summary>
  /// This method will be called after navigating to the view model.
  /// Override this method to perform initialization logic.
  /// </summary>
  Task Initialize(bool forceReinit = false);
}

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

  protected bool IsInitialized => _initialized;

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
  public async Task Initialize(bool forceReinit = false)
  {
    using var lockScope = await _initializeLock.AcquireLockAsync(CancellationToken.None);

    if (_initialized && !forceReinit)
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
