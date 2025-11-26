using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Common.ServiceInterfaces;

namespace ControlR.DesktopClient.ViewModels;

public class ViewModelBase : ObservableObject, IViewModelBase
{
  public virtual Task Initialize()
  {
    return Task.CompletedTask;
  }
}
