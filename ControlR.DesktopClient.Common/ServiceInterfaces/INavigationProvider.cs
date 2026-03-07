using ControlR.DesktopClient.Common.ViewModelInterfaces;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface INavigationProvider
{
  /// <summary>
  ///   Raised when navigation to a new view model occurs.
  /// </summary>
  event Action<Type?>? NavigationOccurred;

  /// <summary>
  /// The currently active view model type, if any.
  /// </summary>
  Type? ActiveViewModel { get; }

  Task NavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task NavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
  void ShowMainWindow();
  Task ShowMainWindowAndNavigateTo<TViewModel>() where TViewModel : IViewModelBase;
  Task ShowMainWindowAndNavigateTo<TViewModel>(TViewModel viewModel) where TViewModel : IViewModelBase;
}
