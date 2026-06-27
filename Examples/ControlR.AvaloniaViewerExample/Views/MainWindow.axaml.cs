using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using System.ComponentModel;
using ControlR.AvaloniaViewerExample.ViewModels;
using ControlR.Viewer.Avalonia;
using ControlR.Viewer.Avalonia.Services;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.AvaloniaViewerExample.Views;

public partial class MainWindow : Window
{
  private IDisposable? _errorContentSubscription;
  private IDisposable? _viewerServicesReadyRegistration;
  private IMainWindowViewModel? _viewModel;

  public MainWindow()
  {
    InitializeComponent();
  }

  protected override void OnClosed(EventArgs e)
  {
    _errorContentSubscription?.Dispose();
    _viewerServicesReadyRegistration?.Dispose();
    _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;
    _viewModel?.Dispose();
    base.OnClosed(e);
  }

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    HandleDataContextChanged();
  }

  private void ApplyTheme(bool isDarkMode)
  {
    var themeVariant = isDarkMode
      ? ThemeVariant.Dark
      : ThemeVariant.Light;

    RequestedThemeVariant = themeVariant;
    Application.Current?.RequestedThemeVariant = themeVariant;
  }

  private void HandleDataContextChanged()
  {
    _viewerServicesReadyRegistration?.Dispose();
    _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;

    _viewModel = DataContext as IMainWindowViewModel;
    if (_viewModel is null)
    {
      return;
    }

    ApplyTheme(_viewModel.IsDarkMode);
    _viewerServicesReadyRegistration = ViewerRegistry.OnServicesReady(Viewer.InstanceId, HandleViewerServicesReady);

    // Sync the viewer's initialization error to the ViewModel.
    _errorContentSubscription?.Dispose();
    _errorContentSubscription = Viewer.GetObservable(ControlrViewer.ErrorContentProperty)
      .Subscribe(HandleViewerErrorContentChanged);

    // Also pick up any error that was set before we subscribed.
    HandleViewerErrorContentChanged(Viewer.ErrorContent);

    _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    SyncSidebarSelection(_viewModel.ActivePage);
  }

  private void HandleViewerErrorContentChanged(string? errorMessage)
  {
    _viewModel?.HandleViewerError(errorMessage);
  }

  private Task HandleViewerServicesReady(ViewerInstanceInfo instanceInfo)
  {
    _viewModel?.RegisterAuthChangeHandler(instanceInfo.InstanceId);
    return Task.CompletedTask;
  }

  private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (sender is not IMainWindowViewModel viewModel)
    {
      return;
    }

    if (e.PropertyName == nameof(IMainWindowViewModel.IsDarkMode))
    {
      ApplyTheme(viewModel.IsDarkMode);

      return;
    }

    if (e.PropertyName == nameof(IMainWindowViewModel.ActivePage))
    {
      SyncSidebarSelection(viewModel.ActivePage);
    }
  }

  private void SidebarNavList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (DataContext is not IMainWindowViewModel viewModel ||
        SidebarNavList.SelectedItem is not ListBoxItem selectedItem)
    {
      return;
    }

    if (selectedItem.Tag is ViewerPage page && viewModel.ActivePage != page)
    {
      viewModel.ActivePage = page;
    }
  }

  private void SyncSidebarSelection(ViewerPage page)
  {
    foreach (var item in SidebarNavList.Items.OfType<ListBoxItem>())
    {
      if (item.Tag is ViewerPage itemPage && itemPage == page)
      {
        if (!ReferenceEquals(SidebarNavList.SelectedItem, item))
        {
          SidebarNavList.SelectedItem = item;
        }

        return;
      }
    }

    SidebarNavList.SelectedItem = null;
  }
}