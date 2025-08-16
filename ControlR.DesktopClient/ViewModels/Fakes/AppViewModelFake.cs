using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace ControlR.DesktopClient.ViewModels.Fakes;
public class AppViewModelFake : IAppViewModel
{
  public ICommand ExitApplicationCommand { get; } = new RelayCommand(() => { });


  public ICommand ShowWindowCommand { get; } = new RelayCommand(() => { });
}
