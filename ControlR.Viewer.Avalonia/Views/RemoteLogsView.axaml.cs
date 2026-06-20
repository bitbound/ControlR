using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ControlR.Viewer.Avalonia.ViewModels;

namespace ControlR.Viewer.Avalonia.Views;

public partial class RemoteLogsView : UserControl
{
  public RemoteLogsView()
  {
    InitializeComponent();
    LogContentTextBox.TextChanged += HandleLogContentTextChanged;
  }

  private IRemoteLogsViewModel? ViewModel => DataContext as IRemoteLogsViewModel;

  private async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is null)
    {
      return;
    }

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel?.Clipboard is not { } clipboard)
    {
      return;
    }

    await ViewModel.CopyContentsToClipboard(clipboard);
  }

  private async void DownloadButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is null)
    {
      return;
    }

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel?.StorageProvider is not { CanSave: true } storageProvider)
    {
      return;
    }

    var suggestedFileName = ViewModel.SelectedFileName ?? "log.txt";
    var destinationFile = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = Assets.Resources.RemoteLogs_DownloadDialogTitle,
      SuggestedFileName = suggestedFileName
    });

    if (destinationFile is null)
    {
      return;
    }

    await ViewModel.DownloadSelectedFile(destinationFile);
  }

  private void FilterTextBox_OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.Key == Key.Enter)
    {
      ViewModel?.ApplyFilter();
    }
  }

  private void HandleLogContentTextChanged(object? sender, TextChangedEventArgs e)
  {
    if (string.IsNullOrEmpty(LogContentTextBox.Text))
    {
      return;
    }

    Dispatcher.UIThread.Post(ScrollLogContentToEnd);
  }

  private async void RefreshContentsButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.RefreshCurrentContents();
    }
  }

  private async void RefreshTreeButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.RefreshTree();
    }
  }

  private void ScrollLogContentToEnd()
  {
    var scrollViewer = LogContentTextBox.FindDescendantOfType<ScrollViewer>();
    if (scrollViewer is null)
    {
      return;
    }

    scrollViewer.ScrollToEnd();
  }
}
