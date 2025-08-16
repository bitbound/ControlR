
namespace ControlR.DesktopClient.ViewModels.Fakes;

internal class ViewModelBaseFake : IViewModelBase
{
  public Task Initialize()
  {
    return Task.CompletedTask;
  }
}
