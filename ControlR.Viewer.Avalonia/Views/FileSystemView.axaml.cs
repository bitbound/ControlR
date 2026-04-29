using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace ControlR.Viewer.Avalonia.Views;

public partial class FileSystemView : UserControl
{
  private const double CompactToolbarThreshold = 500;

  public FileSystemView()
  {
    InitializeComponent();
    FileActionsToolbarGrid.SizeChanged += (_, _) => UpdateToolbarMode();
    UpdateToolbarMode();
  }

  private IFileSystemViewModel? ViewModel => DataContext as IFileSystemViewModel;

  private async void AddressBarTextBox_OnKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.Key != Key.Enter || ViewModel is null)
    {
      return;
    }

    e.Handled = true;
    await ViewModel.NavigateToAddress();
  }

  private async void DeleteButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.DeleteSelection();
    }
  }

  private void DirectoryContentsGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
  {
    if (ViewModel is null)
    {
      return;
    }

    var columnKey = e.Column.SortMemberPath ?? "Name";
    ViewModel.ToggleSort(columnKey);
    e.Handled = true;
  }

  private async void DirectoryContentsGrid_OnTapped(object? sender, TappedEventArgs e)
  {
    if (ViewModel is null || e.Source is not Control source)
    {
      return;
    }

    if (source.FindAncestorOfType<CheckBox>() is not null)
    {
      return;
    }

    if (source.FindAncestorOfType<DataGridRow>()?.DataContext is not FileSystemEntryItemViewModel item)
    {
      return;
    }

    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
    {
      item.IsSelected = !item.IsSelected;
      e.Handled = true;
      return;
    }

    if (!item.IsDirectory)
    {
      return;
    }

    await ViewModel.NavigateToDirectory(item);
    e.Handled = true;
  }

  private async void DownloadButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is null || ViewModel.IsDownloadInProgress)
    {
      return;
    }

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel?.StorageProvider is not { CanSave: true } storageProvider)
    {
      return;
    }

    var suggestedFileName = ViewModel.GetSuggestedDownloadFileName();
    IReadOnlyList<FilePickerFileType>? fileTypeChoices = suggestedFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
      ? [new FilePickerFileType(ControlR.Viewer.Avalonia.Assets.Resources.FileSystem_ZipArchiveFileType) { Patterns = ["*.zip"] }]
      : null;

    var destinationFile = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = ControlR.Viewer.Avalonia.Assets.Resources.FileSystem_SaveDownloadDialogTitle,
      SuggestedFileName = suggestedFileName,
      FileTypeChoices = fileTypeChoices
    });

    if (destinationFile is null)
    {
      return;
    }

    await ViewModel.DownloadSelection(destinationFile);
  }

  private async void NavigateButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.NavigateToAddress();
    }
  }

  private async void NewFolderButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.CreateNewFolder();
    }
  }

  private async void RefreshButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.Refresh();
    }
  }

  private void UpdateToolbarMode()
  {
    if (InlineActionButtons is null || OverflowActionsButton is null || FileActionsToolbarGrid is null)
    {
      return;
    }

    var useCompactToolbar = FileActionsToolbarGrid.Bounds.Width < CompactToolbarThreshold;
    InlineActionButtons.IsVisible = !useCompactToolbar;
    OverflowActionsButton.IsVisible = useCompactToolbar;
  }

  private async void UploadButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is null || ViewModel.IsUploadInProgress)
    {
      return;
    }

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel?.StorageProvider is not { CanOpen: true } storageProvider)
    {
      return;
    }

    var selectedFiles = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = ControlR.Viewer.Avalonia.Assets.Resources.FileSystem_UploadDialogTitle,
      AllowMultiple = true
    });

    await ViewModel.UploadFiles(selectedFiles);
  }

  private async void UpOneLevelButton_OnClick(object? sender, RoutedEventArgs e)
  {
    if (ViewModel is not null)
    {
      await ViewModel.UpOneLevel();
    }
  }
}