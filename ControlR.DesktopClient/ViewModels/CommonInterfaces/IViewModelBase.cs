namespace ControlR.DesktopClient.ViewModels.CommonInterfaces;

public interface IViewModelBase : IDisposable, IAsyncDisposable
{
  /// <summary>
  /// The name of the view associated with this view model.
  /// </summary>
  Type ViewType { get; }

  /// <summary>
  /// This method will be called after navigating to the view model.
  /// Override this method to perform initialization logic.
  /// </summary>
  Task Initialize();
}
