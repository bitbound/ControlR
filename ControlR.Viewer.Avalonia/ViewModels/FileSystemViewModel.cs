using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Concurrent;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.IO;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Viewer.Avalonia.ViewModels.Dialogs;

namespace ControlR.Viewer.Avalonia.ViewModels;

internal interface IFileSystemViewModel : IViewModelBase
{
  string AddressBarValue { get; set; }
  bool CanCreateFolder { get; }
  bool CanDelete { get; }
  bool CanDownload { get; }
  bool CanUpload { get; }
  bool CanUpOneLevel { get; }
  ReadOnlyObservableCollection<FileSystemEntryItemViewModel> DirectoryContents { get; }
  double DownloadProgressPercent { get; }
  string DownloadProgressPercentText { get; }
  string DownloadProgressStatusText { get; }
  string DownloadProgressTitle { get; }
  bool HasRootItems { get; }
  bool HasSelectedPath { get; }
  bool IsDownloadInProgress { get; }
  bool IsDownloadProgressIndeterminate { get; }
  bool IsLoading { get; }
  bool IsLoadingContents { get; }
  bool IsUploadInProgress { get; }
  bool IsUploadProgressIndeterminate { get; }
  ObservableCollection<FileSystemTreeItemViewModel> RootItems { get; }
  string SearchText { get; set; }
  bool ShowNoDrivesMessage { get; }
  bool ShowSelectDirectoryMessage { get; }
  double UploadProgressPercent { get; }
  string UploadProgressPercentText { get; }
  string UploadProgressStatusText { get; }
  string UploadProgressTitle { get; }

  Task CreateNewFolder();
  Task DeleteSelection();
  Task DownloadSelection(IStorageFile destinationFile);
  string GetSuggestedDownloadFileName();
  Task NavigateToAddress();
  Task NavigateToDirectory(FileSystemEntryItemViewModel item);
  Task Refresh();
  void SortBy(string columnKey, ListSortDirection direction);
  void ToggleSort(string columnKey);
  Task UploadFiles(IReadOnlyList<IStorageFile> files);
  Task UpOneLevel();
}

public partial class FileSystemViewModel : ViewModelBase<FileSystemView>, IFileSystemViewModel
{
  private const int TreeTraversalMaxAttempts = 3;

  private readonly IControlrApi _controlrApi;
  private readonly IDeviceState _deviceState;
  private readonly IDialogProvider _dialogProvider;
  private readonly ObservableCollection<FileSystemEntryItemViewModel> _directoryContents = [];
  private readonly List<FileSystemEntryItemViewModel> _directoryContentsSource = [];
  private readonly ILogger<FileSystemViewModel> _logger;
  private readonly ISnackbar _snackbar;
  private readonly ConcurrentDictionary<string, Task> _treeLoadOperations = new(StringComparer.OrdinalIgnoreCase);

  private IReadOnlyList<FileSystemEntryItemViewModel>? _cachedSelectedItems;
  private string? _currentSortColumn = "Name";
  private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;
  private DateTimeOffset _lastUploadObservationTime;
  private long _lastUploadObservedBytes;
  private bool _updatingTreeSelection;

  public FileSystemViewModel(
    IControlrApi controlrApi,
    IDeviceState deviceState,
    IDialogProvider dialogProvider,
    ISnackbar snackbar,
    ILogger<FileSystemViewModel> logger)
  {
    _controlrApi = controlrApi;
    _deviceState = deviceState;
    _dialogProvider = dialogProvider;
    _snackbar = snackbar;
    _logger = logger;
    DirectoryContents = new ReadOnlyObservableCollection<FileSystemEntryItemViewModel>(_directoryContents);
    RootItems.CollectionChanged += (_, _) =>
    {
      OnPropertyChanged(nameof(HasRootItems));
      OnPropertyChanged(nameof(ShowNoDrivesMessage));
    };
  }

  [ObservableProperty]
  public partial string AddressBarValue { get; set; } = string.Empty;
  public bool CanCreateFolder => HasSelectedPath;
  public bool CanDelete => SelectedItems.Count > 0;
  public bool CanDownload => SelectedItems.Count > 0 && !IsDownloadInProgress;
  public bool CanUpload => HasSelectedPath && !IsUploadInProgress;
  public bool CanUpOneLevel => HasSelectedPath && !RootItems.Any(item => string.Equals(item.FullPath, SelectedPath, StringComparison.OrdinalIgnoreCase));
  public ReadOnlyObservableCollection<FileSystemEntryItemViewModel> DirectoryContents { get; }
  [ObservableProperty]
  public partial double DownloadProgressPercent { get; set; }
  [ObservableProperty]
  public partial string DownloadProgressPercentText { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string DownloadProgressStatusText { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string DownloadProgressTitle { get; set; } = string.Empty;
  public bool HasRootItems => RootItems.Count > 0;
  public bool HasSelectedPath => !string.IsNullOrWhiteSpace(SelectedPath);
  [ObservableProperty]
  public partial bool IsDownloadInProgress { get; set; }
  [ObservableProperty]
  public partial bool IsDownloadProgressIndeterminate { get; set; }
  [ObservableProperty]
  public partial bool IsLoading { get; set; }
  [ObservableProperty]
  public partial bool IsLoadingContents { get; set; }
  [ObservableProperty]
  public partial bool IsUploadInProgress { get; set; }
  [ObservableProperty]
  public partial bool IsUploadProgressIndeterminate { get; set; }
  public ObservableCollection<FileSystemTreeItemViewModel> RootItems { get; } = [];
  [ObservableProperty]
  public partial string SearchText { get; set; } = string.Empty;
  public bool ShowNoDrivesMessage => !IsLoading && !HasRootItems;
  public bool ShowSelectDirectoryMessage => !IsLoadingContents && !HasSelectedPath;
  [ObservableProperty]
  public partial double UploadProgressPercent { get; set; }
  [ObservableProperty]
  public partial string UploadProgressPercentText { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string UploadProgressStatusText { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string UploadProgressTitle { get; set; } = string.Empty;

  private IReadOnlyList<FileSystemEntryItemViewModel> SelectedItems => _cachedSelectedItems ??= [.. _directoryContentsSource.Where(item => item.IsSelected)];
  private string? SelectedPath { get; set; }
  private FileSystemSortColumn SortColumn { get; set; } = FileSystemSortColumn.Name;
  private ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;

  public async Task CreateNewFolder()
  {
    if (!HasSelectedPath)
    {
      _snackbar.Add(Assets.Resources.FileSystem_SelectDirectoryFirst, SnackbarSeverity.Warning);
      return;
    }

    var promptViewModel = new TextPromptDialogViewModel(
      _dialogProvider, 
      Assets.Resources.FileSystem_NewFolderSubtitle,
      Assets.Resources.FileSystem_NewFolderLabel,
      Assets.Resources.FileSystem_NewFolderHint,
      Assets.Resources.FileSystem_SubmitButtonText,
      Assets.Resources.FileSystem_CancelButtonText);
      
    _dialogProvider.Show<TextPromptDialogViewModel, Views.Dialogs.TextPromptDialogView>(Assets.Resources.FileSystem_NewFolderDialogTitle, promptViewModel, maxWidth: 520, maxHeight: 260);

    var folderName = await promptViewModel.Completion;
    if (string.IsNullOrWhiteSpace(folderName))
    {
      return;
    }

    try
    {
      var result = await _controlrApi.DeviceFileSystem.CreateDirectory(new CreateDirectoryRequestDto(_deviceState.CurrentDevice.Id, SelectedPath!, folderName));
      if (!result.IsSuccess)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_CreateFolderFailureDetailsMessage, result.Reason), SnackbarSeverity.Error);
        return;
      }

      _snackbar.Add(string.Format(Assets.Resources.FileSystem_CreateFolderSuccessMessage, folderName), SnackbarSeverity.Success);
      await RefreshSelectedDirectory();
      InvalidateCurrentTreeNodeChildren();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating folder {FolderName}.", folderName);
      _snackbar.Add(Assets.Resources.FileSystem_CreateFolderFailureMessage, SnackbarSeverity.Error);
    }
  }

  public async Task DeleteSelection()
  {
    if (SelectedItems.Count == 0)
    {
      _snackbar.Add(Assets.Resources.FileSystem_SelectItemsToDelete, SnackbarSeverity.Warning);
      return;
    }

    var message = SelectedItems.Count == 1
      ? string.Format(Assets.Resources.FileSystem_DeleteSingleMessage, SelectedItems[0].Name)
      : string.Format(Assets.Resources.FileSystem_DeleteMultipleMessage, SelectedItems.Count, string.Join(", ", SelectedItems.Select(item => item.Name)));

    if (!await ShowConfirmation(Assets.Resources.FileSystem_DeleteDialogTitle, message, Assets.Resources.FileSystem_DeleteButtonText, Assets.Resources.FileSystem_CancelButtonText))
    {
      return;
    }

    try
    {
      foreach (var item in SelectedItems)
      {
        var result = await _controlrApi.DeviceFileSystem.DeleteFile(new FileDeleteRequestDto(_deviceState.CurrentDevice.Id, item.FullPath, item.IsDirectory));
        if (!result.IsSuccess)
        {
          throw new InvalidOperationException(result.Reason);
        }
      }

      _snackbar.Add(string.Format(Assets.Resources.FileSystem_DeleteSuccessMessage, SelectedItems.Count), SnackbarSeverity.Success);
      await RefreshSelectedDirectory();
      InvalidateCurrentTreeNodeChildren();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting selected file system items.");
      _snackbar.Add(Assets.Resources.FileSystem_DeleteFailureMessage, SnackbarSeverity.Error);
    }
  }

  public async Task DownloadSelection(IStorageFile destinationFile)
  {
    if (IsDownloadInProgress)
    {
      _snackbar.Add(Assets.Resources.FileSystem_DownloadAlreadyInProgressMessage, SnackbarSeverity.Warning);
      return;
    }

    if (SelectedItems.Count == 0)
    {
      _snackbar.Add(Assets.Resources.FileSystem_SelectItemsToDownload, SnackbarSeverity.Warning);
      return;
    }

    var maxFileSizeResult = await GetMaxFileSize();
    if (!maxFileSizeResult.IsSuccess)
    {
      return;
    }

    var oversizedItems = SelectedItems.Where(item => !item.IsDirectory && item.Size > maxFileSizeResult.Value).ToList();
    if (oversizedItems.Count > 0)
    {
      var maxMb = (double)maxFileSizeResult.Value / (1024 * 1024);
      _snackbar.Add(string.Format(Assets.Resources.FileSystem_DownloadSizeExceededMessage, maxMb, string.Join(", ", oversizedItems.Select(item => item.Name))), SnackbarSeverity.Warning);
      return;
    }

    try
    {
      BeginDownloadProgress(destinationFile.Name);
      await using var destinationStream = await destinationFile.OpenWriteAsync();

      if (SelectedItems.Count == 1 && !SelectedItems[0].IsDirectory)
      {
        var result = await _controlrApi.DeviceFileSystem.DownloadFile(_deviceState.CurrentDevice.Id, SelectedItems[0].FullPath);
        if (!result.IsSuccess || result.Value is null)
        {
          _snackbar.Add(string.Format(Assets.Resources.FileSystem_DownloadFailureDetailsMessage, result.Reason), SnackbarSeverity.Error);
          return;
        }

        await using var responseStream = result.Value;
        await CopyDownloadToDestination(responseStream, destinationStream);
      }
      else
      {
        var request = new DownloadArchiveRequestDto(GetSuggestedDownloadFileName(), SelectedItems.Select(item => item.FullPath).ToArray());
        var result = await _controlrApi.DeviceFileSystem.DownloadArchive(_deviceState.CurrentDevice.Id, request);
        if (!result.IsSuccess || result.Value is null)
        {
          _snackbar.Add(string.Format(Assets.Resources.FileSystem_DownloadFailureDetailsMessage, result.Reason), SnackbarSeverity.Error);
          return;
        }

        await using var responseStream = result.Value;
        await CopyDownloadToDestination(responseStream, destinationStream);
      }

      await destinationStream.FlushAsync();
      _snackbar.Add(string.Format(Assets.Resources.FileSystem_DownloadSavedMessage, destinationFile.Name), SnackbarSeverity.Success);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error downloading selected file system items.");
      _snackbar.Add(Assets.Resources.FileSystem_DownloadFailureMessage, SnackbarSeverity.Error);
    }
    finally
    {
      ResetDownloadProgress();
    }
  }

  public string GetSuggestedDownloadFileName()
  {
    if (SelectedItems.Count == 1)
    {
      var selectedItem = SelectedItems[0];
      return selectedItem.IsDirectory ? $"{selectedItem.Name}.zip" : selectedItem.Name;
    }

    var directoryName = string.IsNullOrWhiteSpace(SelectedPath)
      ? "controlr-download"
      : Path.GetFileName(SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    return $"{directoryName}-download-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip";
  }

  public async Task NavigateToAddress()
  {
    if (string.IsNullOrWhiteSpace(AddressBarValue))
    {
      return;
    }

    if (!await BuildTreeToPath(AddressBarValue.Trim()))
    {
      return;
    }

    await SelectTreePath(AddressBarValue.Trim());
  }

  public Task NavigateToDirectory(FileSystemEntryItemViewModel item)
  {
    AddressBarValue = item.FullPath;
    return NavigateToAddress();
  }

  public async Task Refresh()
  {
    if (!HasSelectedPath)
    {
      await LoadRootDrives();
      return;
    }

    await RefreshSelectedDirectory();
  }

  public void SortBy(string columnKey, ListSortDirection direction)
  {
    _currentSortColumn = columnKey;
    _currentSortDirection = direction;
    SortColumn = columnKey switch
    {
      "Size" => FileSystemSortColumn.Size,
      "LastModified" => FileSystemSortColumn.Modified,
      "Modified" => FileSystemSortColumn.Modified,
      _ => FileSystemSortColumn.Name
    };
    SortDirection = direction;
    RebuildDirectoryContents();
  }

  public void ToggleSort(string columnKey)
  {
    var newDirection = _currentSortColumn == columnKey && _currentSortDirection == ListSortDirection.Ascending
      ? ListSortDirection.Descending
      : ListSortDirection.Ascending;

    SortBy(columnKey, newDirection);
  }

  public async Task UploadFiles(IReadOnlyList<IStorageFile> files)
  {
    if (IsUploadInProgress)
    {
      _snackbar.Add(Assets.Resources.FileSystem_UploadAlreadyInProgressMessage, SnackbarSeverity.Warning);
      return;
    }

    if (!HasSelectedPath)
    {
      _snackbar.Add(Assets.Resources.FileSystem_SelectDirectoryFirst, SnackbarSeverity.Warning);
      return;
    }

    if (files.Count == 0)
    {
      return;
    }

    var maxFileSizeResult = await GetMaxFileSize();
    if (!maxFileSizeResult.IsSuccess)
    {
      return;
    }

    var uploadedAny = false;
    foreach (var file in files)
    {
      try
      {
        await using var fileStream = await file.OpenReadAsync();
        var fileLength = fileStream.Length;
        BeginUploadProgress(file.Name, fileLength);
        await using var observer = new StreamObserver(fileStream, TimeSpan.FromMilliseconds(200));
        using var registration = observer.OnPositionChanged(position => UpdateUploadProgress(position, fileLength));

        if (fileLength > maxFileSizeResult.Value)
        {
          var maxMb = (double)maxFileSizeResult.Value / (1024 * 1024);
          _snackbar.Add(string.Format(Assets.Resources.FileSystem_UploadSizeExceededMessage, file.Name, maxMb), SnackbarSeverity.Warning);
          continue;
        }

        var overwrite = _directoryContentsSource.Any(item => !item.IsDirectory && string.Equals(item.Name, file.Name, StringComparison.OrdinalIgnoreCase));
        if (overwrite)
        {
          var confirmed = await ShowConfirmation(Assets.Resources.FileSystem_OverwriteDialogTitle, string.Format(Assets.Resources.FileSystem_OverwriteDialogMessage, file.Name), Assets.Resources.FileSystem_OverwriteButtonText, Assets.Resources.FileSystem_CancelButtonText);
          if (!confirmed)
          {
            continue;
          }
        }

        var result = await _controlrApi.DeviceFileSystem.UploadFile(_deviceState.CurrentDevice.Id, fileStream, file.Name, SelectedPath!, overwrite);
        if (!result.IsSuccess)
        {
          _snackbar.Add(string.Format(Assets.Resources.FileSystem_UploadFailureDetailsMessage, file.Name, result.Reason), SnackbarSeverity.Error);
          continue;
        }

        await UpdateUploadProgress(fileLength, fileLength);
        uploadedAny = true;
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_UploadSuccessMessage, file.Name), SnackbarSeverity.Success);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error uploading file {FileName}.", file.Name);
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_UploadErrorMessage, file.Name), SnackbarSeverity.Error);
      }
      finally
      {
        ResetUploadProgress();
      }
    }

    if (uploadedAny)
    {
      await RefreshSelectedDirectory();
      InvalidateCurrentTreeNodeChildren();
    }
  }

  public async Task UpOneLevel()
  {
    if (!HasSelectedPath)
    {
      return;
    }

    try
    {
      var result = await _controlrApi.DeviceFileSystem.GetPathSegments(new GetPathSegmentsRequestDto(_deviceState.CurrentDevice.Id, SelectedPath!));
      if (!result.IsSuccess || result.Value is null || !result.Value.Success)
      {
        _snackbar.Add(result.Value?.ErrorMessage is { Length: > 0 }
            ? string.Format(Assets.Resources.FileSystem_UpOneLevelFailureDetailsMessage, result.Value.ErrorMessage)
            : Assets.Resources.FileSystem_UpOneLevelUnavailable,
          SnackbarSeverity.Warning);
        return;
      }

      if (result.Value.PathSegments.Length <= 1)
      {
        return;
      }

      var parentPath = result.Value.PathSegments[0];
      for (var index = 1; index < result.Value.PathSegments.Length - 1; index++)
      {
        parentPath = CombinePaths(parentPath, result.Value.PathSegments[index], result.Value.PathSeparator);
      }

      if (!await BuildTreeToPath(parentPath))
      {
        return;
      }

      await SelectTreePath(parentPath);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error navigating to parent directory.");
      _snackbar.Add(Assets.Resources.FileSystem_UpOneLevelFailureMessage, SnackbarSeverity.Error);
    }
  }

  internal async Task LoadTreeItemChildren(FileSystemTreeItemViewModel item, bool forceReload = false)
  {
    if (item.IsPlaceholder || (!forceReload && !item.HasUnloadedChildren))
    {
      return;
    }

    Task? existingOperation = null;
    if (!forceReload && _treeLoadOperations.TryGetValue(item.FullPath, out existingOperation))
    {
      await existingOperation;
      return;
    }

    var loadOperation = LoadTreeItemChildrenCore(item, forceReload);
    if (!_treeLoadOperations.TryAdd(item.FullPath, loadOperation))
    {
      if (_treeLoadOperations.TryGetValue(item.FullPath, out existingOperation))
      {
        await existingOperation;
      }

      return;
    }

    try
    {
      await loadOperation;
    }
    finally
    {
      _treeLoadOperations.TryRemove(new KeyValuePair<string, Task>(item.FullPath, loadOperation));
    }
  }

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    await LoadRootDrives();
  }

  private static void ClearTreeSelection(IEnumerable<FileSystemTreeItemViewModel> items)
  {
    foreach (var item in items)
    {
      item.IsSelected = false;
      ClearTreeSelection(item.Children.Where(child => !child.IsPlaceholder));
    }
  }

  private static string CombinePaths(string path1, string path2, string pathSeparator)
  {
    return $"{path1.TrimEnd(pathSeparator.ToCharArray())}{pathSeparator}{path2.TrimStart(pathSeparator.ToCharArray())}";
  }

  private static FileSystemEntryItemViewModel ConvertToEntryItem(FileSystemEntryDto dto)
  {
    return new FileSystemEntryItemViewModel
    {
      Name = dto.Name,
      FullPath = dto.FullPath,
      IsDirectory = dto.IsDirectory,
      IsHidden = dto.IsHidden,
      LastModified = dto.LastModified,
      Size = dto.Size,
      CanRead = dto.CanRead,
      CanWrite = dto.CanWrite
    };
  }

  private static FileSystemTreeItemViewModel ConvertToTreeItem(FileSystemEntryDto dto)
  {
    return new FileSystemTreeItemViewModel(dto.Name, dto.FullPath, dto.IsDirectory && dto.HasSubfolders);
  }

  private static FileSystemTreeItemViewModel? FindTreeItem(IEnumerable<FileSystemTreeItemViewModel> items, string path)
  {
    foreach (var item in items)
    {
      if (string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))
      {
        return item;
      }

      var child = FindTreeItem(item.Children.Where(childItem => !childItem.IsPlaceholder), path);
      if (child is not null)
      {
        return child;
      }
    }

    return null;
  }

  private void BeginDownloadProgress(string fileName)
  {
    IsDownloadInProgress = true;
    IsDownloadProgressIndeterminate = true;
    DownloadProgressPercent = 0;
    DownloadProgressPercentText = string.Empty;
    DownloadProgressStatusText = Assets.Resources.FileSystem_DownloadPreparingMessage;
    DownloadProgressTitle = string.Format(Assets.Resources.FileSystem_DownloadProgressTitle, fileName);
  }

  private void BeginUploadProgress(string fileName, long totalBytes)
  {
    IsUploadInProgress = true;
    IsUploadProgressIndeterminate = false;
    UploadProgressPercent = 0;
    UploadProgressPercentText = string.Format(CultureInfo.CurrentCulture, "{0:F0}%", 0d);
    UploadProgressStatusText = string.Format(
      Assets.Resources.FileSystem_UploadProgressStatusMessage,
      UnitsHelper.ToHumanReadableFileSize(0),
      UnitsHelper.ToHumanReadableFileSize(totalBytes));
    UploadProgressTitle = string.Format(Assets.Resources.FileSystem_UploadProgressTitle, fileName);
    _lastUploadObservedBytes = 0;
    _lastUploadObservationTime = DateTimeOffset.UtcNow;
  }

  private async Task<bool> BuildTreeToPath(string targetPath)
  {
    try
    {
      var result = await _controlrApi.DeviceFileSystem.GetPathSegments(new GetPathSegmentsRequestDto(_deviceState.CurrentDevice.Id, targetPath));
      if (!result.IsSuccess || result.Value is null)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_PathValidationErrorMessage, targetPath), SnackbarSeverity.Error);
        return false;
      }

      if (!result.Value.Success)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_PathValidationErrorDetailsMessage, targetPath, result.Value.ErrorMessage), SnackbarSeverity.Error);
        return false;
      }

      if (!result.Value.PathExists)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_PathNotFoundMessage, targetPath), SnackbarSeverity.Warning);
        return false;
      }

      for (var attempt = 0; attempt < TreeTraversalMaxAttempts; attempt++)
      {
        var currentPath = result.Value.PathSegments[0];
        var currentItems = RootItems.AsEnumerable();
        var traversalCompleted = true;

        for (var index = 1; index < result.Value.PathSegments.Length; index++)
        {
          var treeItem = FindTreeItem(currentItems, currentPath);
          if (treeItem is null)
          {
            traversalCompleted = false;
            break;
          }

          treeItem.IsExpanded = true;
          await LoadTreeItemChildren(treeItem, forceReload: attempt > 0);

          currentPath = CombinePaths(currentPath, result.Value.PathSegments[index], result.Value.PathSeparator);
          currentItems = treeItem.Children.Where(child => !child.IsPlaceholder);

          if (FindTreeItem(currentItems, currentPath) is null)
          {
            traversalCompleted = false;
            break;
          }
        }

        if (traversalCompleted && FindTreeItem(RootItems, targetPath) is not null)
        {
          return true;
        }
      }

      _snackbar.Add(string.Format(Assets.Resources.FileSystem_NavigateErrorMessage, targetPath), SnackbarSeverity.Error);
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error building tree to path {Path}.", targetPath);
      _snackbar.Add(string.Format(Assets.Resources.FileSystem_NavigateErrorMessage, targetPath), SnackbarSeverity.Error);
      return false;
    }
  }

  private void ClearDirectoryEntries()
  {
    foreach (var entry in _directoryContentsSource)
    {
      entry.PropertyChanged -= HandleDirectoryEntryPropertyChanged;
    }

    _directoryContentsSource.Clear();
    _cachedSelectedItems = null;
  }

  private async Task CopyDownloadToDestination(ResponseStream responseStream, Stream destinationStream)
  {
    var totalBytes = responseStream.Response.Content.Headers.ContentLength;
    UpdateDownloadProgress(0, totalBytes);

    var buffer = new byte[81_920];
    var bytesCopied = 0L;
    var lastReportedBytes = 0L;
    var stopwatch = Stopwatch.StartNew();

    while (true)
    {
      var bytesRead = await responseStream.Stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
      if (bytesRead == 0)
      {
        break;
      }

      await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead));
      bytesCopied += bytesRead;

      if (bytesCopied == totalBytes
          || bytesCopied - lastReportedBytes >= 262_144
          || stopwatch.ElapsedMilliseconds >= 200)
      {
        UpdateDownloadProgress(bytesCopied, totalBytes);
        lastReportedBytes = bytesCopied;
        stopwatch.Restart();
      }
    }

    UpdateDownloadProgress(bytesCopied, totalBytes);
  }

  private async Task<Result<long>> GetMaxFileSize()
  {
    var result = await _controlrApi.UserServerSettings.GetFileUploadMaxSize();
    if (!result.IsSuccess)
    {
      _snackbar.Add(Assets.Resources.FileSystem_MaxTransferSizeFailureMessage, SnackbarSeverity.Error);
      return Result.Fail<long>(result.Reason);
    }

    return Result.Ok(result.Value < 0 ? long.MaxValue : result.Value);
  }

  private void HandleDirectoryEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(FileSystemEntryItemViewModel.IsSelected))
    {
      return;
    }

    _cachedSelectedItems = null;

    OnPropertyChanged(nameof(CanDelete));
    OnPropertyChanged(nameof(CanDownload));
  }

  private void HandleTreeItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (sender is not FileSystemTreeItemViewModel item || item.IsPlaceholder)
    {
      return;
    }

    if (e.PropertyName == nameof(FileSystemTreeItemViewModel.IsExpanded) && item.IsExpanded)
    {
      LoadTreeItemChildren(item).Forget();
      return;
    }

    if (e.PropertyName == nameof(FileSystemTreeItemViewModel.IsSelected) && item.IsSelected && !_updatingTreeSelection)
    {
      if (string.Equals(item.FullPath, SelectedPath, StringComparison.OrdinalIgnoreCase))
      {
        return;
      }
      SelectTreePath(item.FullPath).Forget();
    }
  }

  private void InvalidateCurrentTreeNodeChildren()
  {
    if (SelectedPath is null)
    {
      return;
    }

    var currentItem = FindTreeItem(RootItems, SelectedPath);
    currentItem?.SetExpandable(true);
    currentItem?.ResetChildrenPlaceholder();
  }

  private async Task LoadDirectoryContents(string directoryPath)
  {
    try
    {
      IsLoadingContents = true;
      ClearDirectoryEntries();

      var result = await _controlrApi.DeviceFileSystem.GetDirectoryContents(new GetDirectoryContentsRequestDto(_deviceState.CurrentDevice.Id, directoryPath));
      if (!result.IsSuccess || result.Value is null)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_LoadContentsFailureDetailsMessage, result.Reason), SnackbarSeverity.Warning);
        RebuildDirectoryContents();
        return;
      }

      if (!result.Value.DirectoryExists)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_DirectoryNotFoundMessage, directoryPath), SnackbarSeverity.Warning);
        RebuildDirectoryContents();
        return;
      }

      foreach (var entry in result.Value.Entries.Select(ConvertToEntryItem))
      {
        entry.PropertyChanged += HandleDirectoryEntryPropertyChanged;
        _directoryContentsSource.Add(entry);
      }

      RebuildDirectoryContents();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading directory contents for {Path}.", directoryPath);
      ClearDirectoryEntries();
      RebuildDirectoryContents();
      _snackbar.Add(Assets.Resources.FileSystem_LoadContentsFailureMessage, SnackbarSeverity.Error);
    }
    finally
    {
      IsLoadingContents = false;
      OnPropertyChanged(nameof(ShowSelectDirectoryMessage));
    }
  }

  private async Task LoadRootDrives()
  {
    try
    {
      IsLoading = true;
      RootItems.Clear();
      var result = await _controlrApi.DeviceFileSystem.GetRootDrives(new GetRootDrivesRequestDto(_deviceState.CurrentDevice.Id));
      if (!result.IsSuccess || result.Value is null)
      {
        _snackbar.Add(string.Format(Assets.Resources.FileSystem_LoadDrivesFailureDetailsMessage, result.Reason), SnackbarSeverity.Error);
        return;
      }

      foreach (var drive in result.Value.Drives.Where(drive => drive.IsDirectory))
      {
        var item = ConvertToTreeItem(drive);
        SubscribeToTreeItem(item);
        RootItems.Add(item);
      }

      if (RootItems.Count > 0)
      {
        await SelectTreePath(RootItems[0].FullPath);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading root drives.");
      _snackbar.Add(Assets.Resources.FileSystem_LoadDrivesFailureMessage, SnackbarSeverity.Error);
    }
    finally
    {
      IsLoading = false;
      OnPropertyChanged(nameof(ShowNoDrivesMessage));
    }
  }

  private async Task LoadTreeItemChildrenCore(FileSystemTreeItemViewModel item, bool forceReload)
  {
    if (!forceReload && !item.HasUnloadedChildren)
    {
      return;
    }

    try
    {
      var result = await _controlrApi.DeviceFileSystem.GetSubdirectories(new GetSubdirectoriesRequestDto(_deviceState.CurrentDevice.Id, item.FullPath));
      if (!result.IsSuccess || result.Value is null)
      {
        return;
      }

      var children = result.Value.Subdirectories.Select(ConvertToTreeItem).ToArray();
      foreach (var child in children)
      {
        SubscribeToTreeItem(child);
      }

      item.ReplaceChildren(children);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading subdirectories for {Path}.", item.FullPath);
    }
  }

  partial void OnIsDownloadInProgressChanged(bool value)
  {
    OnPropertyChanged(nameof(CanDownload));
  }

  partial void OnIsUploadInProgressChanged(bool value)
  {
    OnPropertyChanged(nameof(CanUpload));
  }

  partial void OnSearchTextChanged(string value)
  {
    RebuildDirectoryContents();
  }

  private void RebuildDirectoryContents()
  {
    _directoryContents.Clear();
    var query = _directoryContentsSource.Where(item => item.MatchesSearch(SearchText));
    query = SortColumn switch
    {
      FileSystemSortColumn.Size => SortDirection == ListSortDirection.Ascending
        ? query.OrderBy(item => !item.IsDirectory).ThenBy(item => item.Size).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        : query.OrderBy(item => !item.IsDirectory).ThenByDescending(item => item.Size).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
      FileSystemSortColumn.Modified => SortDirection == ListSortDirection.Ascending
        ? query.OrderBy(item => !item.IsDirectory).ThenBy(item => item.LastModified).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        : query.OrderBy(item => !item.IsDirectory).ThenByDescending(item => item.LastModified).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
      _ => SortDirection == ListSortDirection.Ascending
        ? query.OrderBy(item => !item.IsDirectory).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        : query.OrderBy(item => !item.IsDirectory).ThenByDescending(item => item.Name, StringComparer.OrdinalIgnoreCase)
    };

    foreach (var item in query)
    {
      _directoryContents.Add(item);
    }
  }

  private async Task RefreshSelectedDirectory()
  {
    if (SelectedPath is not null)
    {
      await LoadDirectoryContents(SelectedPath);
    }
  }

  private void ResetDownloadProgress()
  {
    IsDownloadInProgress = false;
    IsDownloadProgressIndeterminate = true;
    DownloadProgressPercent = 0;
    DownloadProgressPercentText = string.Empty;
    DownloadProgressStatusText = string.Empty;
    DownloadProgressTitle = string.Empty;
  }

  private void ResetUploadProgress()
  {
    IsUploadInProgress = false;
    IsUploadProgressIndeterminate = false;
    UploadProgressPercent = 0;
    UploadProgressPercentText = string.Empty;
    UploadProgressStatusText = string.Empty;
    UploadProgressTitle = string.Empty;
    _lastUploadObservedBytes = 0;
    _lastUploadObservationTime = default;
  }

  private async Task SelectTreePath(string targetPath)
  {
    var treeItem = FindTreeItem(RootItems, targetPath);
    if (treeItem is null)
    {
      return;
    }

    _updatingTreeSelection = true;
    try
    {
      ClearTreeSelection(RootItems);
      treeItem.IsSelected = true;
    }
    finally
    {
      _updatingTreeSelection = false;
    }

    SelectedPath = treeItem.FullPath;
    AddressBarValue = treeItem.FullPath;
    OnPropertyChanged(nameof(HasSelectedPath));
    OnPropertyChanged(nameof(CanCreateFolder));
    OnPropertyChanged(nameof(CanDelete));
    OnPropertyChanged(nameof(CanDownload));
    OnPropertyChanged(nameof(CanUpOneLevel));
    OnPropertyChanged(nameof(CanUpload));
    OnPropertyChanged(nameof(ShowSelectDirectoryMessage));
    await RefreshSelectedDirectory();
  }

  private async Task<bool> ShowConfirmation(string title, string message, string confirmText, string cancelText)
  {
    var dialogViewModel = new ConfirmationDialogViewModel(_dialogProvider, message, confirmText, cancelText);
    _dialogProvider.Show<ConfirmationDialogViewModel, Views.Dialogs.ConfirmationDialogView>(title, dialogViewModel, maxWidth: 520, maxHeight: 240);
    return await dialogViewModel.Completion;
  }

  private void SubscribeToTreeItem(FileSystemTreeItemViewModel item)
  {
    item.PropertyChanged += HandleTreeItemPropertyChanged;
  }

  private void UpdateDownloadProgress(long bytesCopied, long? totalBytes)
  {
    if (totalBytes is > 0)
    {
      var progress = Math.Min(100, bytesCopied * 100d / totalBytes.Value);
      DownloadProgressPercent = progress;
      DownloadProgressPercentText = string.Format(CultureInfo.CurrentCulture, "{0:F0}%", progress);
      DownloadProgressStatusText = string.Format(
        Assets.Resources.FileSystem_DownloadProgressKnownSizeMessage,
        UnitsHelper.ToHumanReadableFileSize(bytesCopied),
        UnitsHelper.ToHumanReadableFileSize(totalBytes.Value));
      IsDownloadProgressIndeterminate = false;
      return;
    }

    DownloadProgressPercent = 0;
    DownloadProgressPercentText = string.Empty;
    DownloadProgressStatusText = string.Format(
      Assets.Resources.FileSystem_DownloadProgressUnknownSizeMessage,
      UnitsHelper.ToHumanReadableFileSize(bytesCopied));
    IsDownloadProgressIndeterminate = true;
  }

  private async Task UpdateUploadProgress(long bytesUploaded, long totalBytes)
  {
    var now = DateTimeOffset.UtcNow;
    var bytesPerSecond = 0d;

    if (_lastUploadObservationTime != default)
    {
      var elapsedSeconds = (now - _lastUploadObservationTime).TotalSeconds;
      if (elapsedSeconds > 0)
      {
        bytesPerSecond = (bytesUploaded - _lastUploadObservedBytes) / elapsedSeconds;
      }
    }

    _lastUploadObservedBytes = bytesUploaded;
    _lastUploadObservationTime = now;

    var progress = totalBytes <= 0 ? 0 : Math.Min(100, bytesUploaded * 100d / totalBytes);

    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      UploadProgressPercent = progress;
      UploadProgressPercentText = string.Format(CultureInfo.CurrentCulture, "{0:F0}%", progress);
      UploadProgressStatusText = string.Format(
        Assets.Resources.FileSystem_UploadProgressStatusMessage,
        UnitsHelper.ToHumanReadableFileSize(bytesUploaded),
        UnitsHelper.ToHumanReadableFileSize(totalBytes));

      if (bytesPerSecond > 0)
      {
        UploadProgressStatusText = string.Format(
          Assets.Resources.FileSystem_UploadProgressStatusWithSpeedMessage,
          UnitsHelper.ToHumanReadableFileSize(bytesUploaded),
          UnitsHelper.ToHumanReadableFileSize(totalBytes),
          UnitsHelper.ToHumanReadableNetworkSpeed(bytesPerSecond));
      }
    });
  }

  private enum FileSystemSortColumn
  {
    Name,
    Size,
    Modified
  }
}