using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.Libraries.Viewer.Avalonia.Views;

public interface IViewModelBase
{
  /// <summary>
  /// This method will be called after navigating to the view model.
  /// Override this method to perform initialization logic.
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
