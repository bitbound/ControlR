
namespace ControlR.Viewer.Avalonia.ViewModels.Fakes;

internal interface IViewModelBaseFake
{
}

internal class ViewModelBaseFake<TView> : IViewModelBase, IViewModelBaseFake
{
  public Type ViewType { get; } = typeof(TView);
  public void Dispose()
  {
    // No-op.
  }

  public ValueTask DisposeAsync()
  {
    return ValueTask.CompletedTask;
  }

  public Task Initialize(bool forceReinit = false)
  {
    return Task.CompletedTask;
  }
}
