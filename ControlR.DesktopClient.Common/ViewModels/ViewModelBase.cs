using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common.ViewModelInterfaces;
using ControlR.Libraries.Shared.Collections;
using ControlR.Libraries.Shared.Extensions;

namespace ControlR.DesktopClient.Common.ViewModels;

public abstract class ViewModelBase<TView> : ObservableObject, IViewModelBase
{
  private readonly SemaphoreSlim _initializeLock = new(1, 1);
  private bool _disposedValue;
  private bool _initialized;

  public Type ViewType { get; } = typeof(TView);

  protected DisposableCollection Disposables { get; } = [];

  public void Dispose()
  {
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  public async ValueTask DisposeAsync()
  {
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
    if (_disposedValue)
    {
      return;
    }

    if (disposing)
    {
      Disposables.Dispose();
      _initializeLock.Dispose();
    }

    _disposedValue = true;
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
