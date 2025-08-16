using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.DesktopClient.Services;

namespace ControlR.DesktopClient.ViewModels;

public interface IViewModelBase
{
  /// <summary>
  /// This method will be called by the <see cref="INavigationProvider"/>
  /// after navigating to the view model.  Override this method to perform
  /// initialization logic.
  /// </summary>
  Task Initialize();
}

public class ViewModelBase : ObservableObject, IViewModelBase
{
  public virtual Task Initialize()
  {
    return Task.CompletedTask;
  }
}
