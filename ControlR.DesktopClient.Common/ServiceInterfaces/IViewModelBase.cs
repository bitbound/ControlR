namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IViewModelBase
{
  /// <summary>
  /// This method will be called after navigating to the view model.
  /// Override this method to perform initialization logic.
  /// </summary>
  Task Initialize();
}
