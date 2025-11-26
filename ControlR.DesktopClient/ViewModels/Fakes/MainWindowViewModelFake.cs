namespace ControlR.DesktopClient.ViewModels.Fakes;
internal class MainWindowViewModelFake : ViewModelBaseFake, IMainWindowViewModel
{
  public IViewModelBase CurrentViewModel { get; set; } = new ManagedDeviceViewModelFake();

  public void HandleMainWindowClosed()
  {
    // Do nothing
  }
}