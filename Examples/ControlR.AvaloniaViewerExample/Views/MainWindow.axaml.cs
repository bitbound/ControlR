using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using System.ComponentModel;
using ControlR.AvaloniaViewerExample.ViewModels;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.AvaloniaViewerExample.Views;

public partial class MainWindow : Window
{
  private IMainWindowViewModel? _viewModel;

  public MainWindow()
  {
    InitializeComponent();
    DataContextChanged += HandleDataContextChanged;
  }
  
  protected override void OnClosed(EventArgs e)
  {
    _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;
    base.OnClosed(e);
  }

  private void ApplyTheme(bool isDarkMode)
  {
    var themeVariant = isDarkMode
      ? ThemeVariant.Dark
      : ThemeVariant.Light;

    RequestedThemeVariant = themeVariant;

    if (Application.Current is not null)
    {
      Application.Current.RequestedThemeVariant = themeVariant;
    }
  }

  private void ConnectButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    ConnectPanel.IsVisible  = false;
    Viewer.IsVisible = true;
  }

  private void HandleDataContextChanged(object? sender, EventArgs e)
  {
    _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;

    _viewModel = DataContext as IMainWindowViewModel;
    if (_viewModel is null)
    {
      return;
    }

    ApplyTheme(_viewModel.IsDarkMode);

    _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    SyncSidebarSelection(_viewModel.ActivePage);
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
    if (DataContext is not IMainWindowViewModel viewModel || SidebarNavList.SelectedItem is not ListBoxItem selectedItem)
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