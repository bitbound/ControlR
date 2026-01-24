
namespace ControlR.DesktopClient.ViewModels.Fakes;

internal class ViewModelBaseFake<TView> : IViewModelBase
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

  public Task Initialize()
  {
    return Task.CompletedTask;
  }
}
