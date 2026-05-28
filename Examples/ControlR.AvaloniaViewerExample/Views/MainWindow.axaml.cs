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
  }

  protected override void OnClosed(EventArgs e)
  {
    _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;
    _viewModel?.Dispose();
    base.OnClosed(e);
  }

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    HandleDataContextChanged();
  }

  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);
    AttachViewer();
  }

  private void ApplyTheme(bool isDarkMode)
  {
    var themeVariant = isDarkMode
      ? ThemeVariant.Dark
      : ThemeVariant.Light;

    RequestedThemeVariant = themeVariant;
    Application.Current?.RequestedThemeVariant = themeVariant;
  }

  private void AttachViewer()
  {
    _viewModel?.AttachViewer(Viewer.InstanceId);
  }

  private void HandleDataContextChanged()
  {
    _viewModel?.PropertyChanged -= HandleViewModelPropertyChanged;

    _viewModel = DataContext as IMainWindowViewModel;
    if (_viewModel is null)
    {
      return;
    }

    ApplyTheme(_viewModel.IsDarkMode);
    AttachViewer();

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