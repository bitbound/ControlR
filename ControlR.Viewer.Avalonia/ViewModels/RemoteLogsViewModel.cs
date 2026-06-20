using System.ComponentModel;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.Viewer.Common.Helpers;

namespace ControlR.Viewer.Avalonia.ViewModels;

public interface IRemoteLogsViewModel : IViewModelBase
{
  string DisplayedLogContents { get; }
  string FilterText { get; set; }
  bool HasRootItems { get; }
  bool IsCopyButtonEnabled { get; }
  bool IsDownloadButtonEnabled { get; }
  bool IsLoading { get; set; }
  bool IsLoadingContents { get; set; }
  bool IsRefreshContentsButtonEnabled { get; }
  string LogContents { get; set; }
  ObservableCollection<LogFilesTreeItemViewModel> RootItems { get; }
  string? SelectedFileName { get; }
  bool ShowLogContentTextBox { get; }
  bool ShowNoContentMessage { get; }
  bool ShowNoFilterMatchesMessage { get; }
  bool ShowNoLogFilesMessage { get; }
  bool ShowSelectLogFilePrompt { get; }

  void ApplyFilter();
  Task CopyContentsToClipboard(IClipboard clipboard);
  Task DownloadSelectedFile(IStorageFile destinationFile);
  Task RefreshCurrentContents();
  Task RefreshTree();
  Task SelectNode(LogFilesTreeItemViewModel? node);
}

public partial class RemoteLogsViewModel : ViewModelBase<RemoteLogsView>, IRemoteLogsViewModel
{
  private readonly IControlrApi _controlrApi;
  private readonly IDeviceState _deviceState;
  private readonly ILogger<RemoteLogsViewModel> _logger;
  private readonly ISnackbar _snackbar;

  private string _displayedLogContents = string.Empty;
  private CancellationTokenSource? _filterDebounceCts;
  private TimeSpan _filterDebounceInterval = TimeSpan.FromMilliseconds(500);
  private CancellationTokenSource? _loadCts;
  private LogFilesTreeItemViewModel? _selectedNode;
  private bool _updatingTreeSelection;

  public RemoteLogsViewModel(
    IControlrApi controlrApi,
    IDeviceState deviceState,
    ISnackbar snackbar,
    ILogger<RemoteLogsViewModel> logger)
  {
    _controlrApi = controlrApi;
    _deviceState = deviceState;
    _snackbar = snackbar;
    _logger = logger;

    RootItems.CollectionChanged += (_, _) => OnRootItemsChanged();
  }

  public string DisplayedLogContents => _displayedLogContents;
  [ObservableProperty]
  public partial string FilterText { get; set; } = string.Empty;
  public bool HasRootItems => RootItems.Count > 0;
  public bool IsCopyButtonEnabled => !string.IsNullOrEmpty(LogContents) && !IsLoadingContents;
  public bool IsDownloadButtonEnabled => !string.IsNullOrEmpty(LogContents) && !IsLoadingContents;
  [ObservableProperty]
  public partial bool IsLoading { get; set; } = true;
  [ObservableProperty]
  public partial bool IsLoadingContents { get; set; }
  public bool IsRefreshContentsButtonEnabled => _selectedNode is not null && _selectedNode.IsFile && !IsLoadingContents;
  [ObservableProperty]
  public partial string LogContents { get; set; } = string.Empty;
  public ObservableCollection<LogFilesTreeItemViewModel> RootItems { get; } = [];
  public string? SelectedFileName => _selectedNode?.IsFile == true ? _selectedNode.Name : null;
  public bool ShowLogContentTextBox => !ShowSelectLogFilePrompt && !ShowNoFilterMatchesMessage;
  public bool ShowNoContentMessage =>
    !IsLoadingContents && _selectedNode is not null && _selectedNode.IsFile && string.IsNullOrEmpty(LogContents);
  public bool ShowNoFilterMatchesMessage =>
    !IsLoadingContents
    && _selectedNode is not null
    && _selectedNode.IsFile
    && !string.IsNullOrEmpty(LogContents)
    && !string.IsNullOrWhiteSpace(FilterText)
    && string.IsNullOrEmpty(DisplayedLogContents);
  public bool ShowNoLogFilesMessage => !IsLoading && !HasRootItems;
  public bool ShowSelectLogFilePrompt =>
    !IsLoadingContents && (_selectedNode is null || !_selectedNode.IsFile);

  public void ApplyFilter()
  {
    _filterDebounceCts?.Cancel();
    _filterDebounceCts?.Dispose();
    _filterDebounceCts = null;
    RefreshDisplayedContents();
  }

  public async Task CopyContentsToClipboard(IClipboard clipboard)
  {
    if (string.IsNullOrEmpty(LogContents))
    {
      return;
    }

    try
    {
      await clipboard.SetTextAsync(LogContents);
      _snackbar.Add(Resources.RemoteLogs_CopySuccess, SnackbarSeverity.Success);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error copying log contents to clipboard.");
      _snackbar.Add(Resources.RemoteLogs_CopyError, SnackbarSeverity.Error);
    }
  }

  public async Task DownloadSelectedFile(IStorageFile destinationFile)
  {
    if (string.IsNullOrEmpty(LogContents))
    {
      _snackbar.Add(Resources.RemoteLogs_DownloadNoSelection, SnackbarSeverity.Warning);
      return;
    }

    try
    {
      await using var destinationStream = await destinationFile.OpenWriteAsync();
      using var writer = new StreamWriter(destinationStream);
      await writer.WriteAsync(LogContents);
      await writer.FlushAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error downloading log contents.");
      _snackbar.Add(Resources.RemoteLogs_DownloadError, SnackbarSeverity.Error);
    }
  }

  public Task RefreshCurrentContents()
  {
    if (_selectedNode is null || !_selectedNode.IsFile)
    {
      return Task.CompletedTask;
    }

    return LoadLogContentsCore(_selectedNode);
  }

  public Task RefreshTree()
  {
    return LoadLogFiles();
  }

  public async Task SelectNode(LogFilesTreeItemViewModel? node)
  {
    if (_updatingTreeSelection)
    {
      return;
    }

    if (ReferenceEquals(_selectedNode, node))
    {
      return;
    }

    _updatingTreeSelection = true;
    try
    {
      ClearTreeSelection(RootItems, _selectedNode);
      if (node is not null)
      {
        node.IsSelected = true;
      }
    }
    finally
    {
      _updatingTreeSelection = false;
    }

    _selectedNode = node;
    OnPropertyChanged(nameof(ShowSelectLogFilePrompt));
    OnPropertyChanged(nameof(ShowNoContentMessage));
    OnPropertyChanged(nameof(ShowNoFilterMatchesMessage));
    OnPropertyChanged(nameof(ShowLogContentTextBox));
    OnPropertyChanged(nameof(IsRefreshContentsButtonEnabled));
    OnPropertyChanged(nameof(SelectedFileName));

    if (node is null || !node.IsFile)
    {
      LogContents = string.Empty;
      return;
    }

    await LoadLogContentsCore(node);
  }

  protected override async Task OnInitializeAsync()
  {
    await base.OnInitializeAsync();
    await LoadLogFiles();
  }

  private static void ClearTreeSelection(
    IEnumerable<LogFilesTreeItemViewModel> items,
    LogFilesTreeItemViewModel? except)
  {
    foreach (var item in items)
    {
      if (!ReferenceEquals(item, except))
      {
        item.IsSelected = false;
      }

      ClearTreeSelection(item.Children, except);
    }
  }

  private static LogFilesTreeItemViewModel? FindSelectedNode(IEnumerable<LogFilesTreeItemViewModel> items)
  {
    foreach (var item in items)
    {
      if (item.IsSelected && item.IsFile)
      {
        return item;
      }

      var child = FindSelectedNode(item.Children);
      if (child is not null)
      {
        return child;
      }
    }

    return null;
  }

  private void HandleTreeItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(LogFilesTreeItemViewModel.IsSelected) || _updatingTreeSelection)
    {
      return;
    }

    if (sender is not LogFilesTreeItemViewModel item || !item.IsSelected)
    {
      return;
    }

    _ = Dispatcher.UIThread.InvokeAsync(async () =>
    {
      try
      {
        var selected = item.IsFile ? item : FindSelectedNode(item.Children);
        if (selected is not null)
        {
          await SelectNode(selected);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error loading selected log file.");
        _snackbar.Add("An error occurred while loading the log file", SnackbarSeverity.Error);
      }
    });
  }

  private async Task LoadLogContentsCore(LogFilesTreeItemViewModel node)
  {
    if (node.FullPath is null)
    {
      LogContents = string.Empty;
      return;
    }

    _loadCts?.Cancel();
    _loadCts?.Dispose();
    _loadCts = new CancellationTokenSource();
    var token = _loadCts.Token;

    try
    {
      IsLoadingContents = true;
      var request = new GetLogFileContentsRequestDto(node.FullPath);
      var result = await _controlrApi.DeviceFileSystem.GetLogFileContents(_deviceState.CurrentDevice.Id, request);

      if (token.IsCancellationRequested)
      {
        return;
      }

      if (!result.IsSuccess)
      {
        _logger.LogError("Failed to load log file contents: {Error}", result.Reason);
        _snackbar.Add(string.Format(Resources.RemoteLogs_LoadContentsFailure, result.Reason), SnackbarSeverity.Error);
        LogContents = string.Empty;
        return;
      }

      LogContents = result.Value;
    }
    catch (Exception ex)
    {
      if (token.IsCancellationRequested)
      {
        return;
      }

      _logger.LogError(ex, "Error loading log file contents.");
      _snackbar.Add(Resources.RemoteLogs_LoadContentsError, SnackbarSeverity.Error);
      LogContents = string.Empty;
    }
    finally
    {
      if (!token.IsCancellationRequested)
      {
        IsLoadingContents = false;
        OnPropertyChanged(nameof(ShowSelectLogFilePrompt));
        OnPropertyChanged(nameof(ShowNoContentMessage));
        OnPropertyChanged(nameof(ShowNoFilterMatchesMessage));
        OnPropertyChanged(nameof(ShowLogContentTextBox));
        OnPropertyChanged(nameof(IsCopyButtonEnabled));
        OnPropertyChanged(nameof(IsDownloadButtonEnabled));
        OnPropertyChanged(nameof(IsRefreshContentsButtonEnabled));
      }
    }
  }

  private async Task LoadLogFiles()
  {
    try
    {
      IsLoading = true;
      UnsubscribeAllTreeItems();
      RootItems.Clear();

      var result = await _controlrApi.DeviceFileSystem.GetLogFiles(_deviceState.CurrentDevice.Id);
      if (!result.IsSuccess || result.Value is null)
      {
        _logger.LogError("Failed to load log files: {Error}", result.Reason);
        _snackbar.Add(string.Format(Resources.RemoteLogs_LoadFilesFailure, result.Reason), SnackbarSeverity.Error);
        return;
      }

      var responseDto = result.Value;
      foreach (var group in responseDto.LogFileGroups)
      {
        var groupNode = new LogFilesTreeItemViewModel(group.GroupName, fullPath: null, isFile: false)
        {
          IsExpanded = true,
        };

        foreach (var file in group.LogFiles)
        {
          var fileNode = new LogFilesTreeItemViewModel(file.FileName, file.FullPath, isFile: true);
          SubscribeToTreeItem(fileNode);
          groupNode.Children.Add(fileNode);
        }

        SubscribeToTreeItem(groupNode);
        RootItems.Add(groupNode);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error loading log files.");
      _snackbar.Add(Resources.RemoteLogs_LoadFilesError, SnackbarSeverity.Error);
    }
    finally
    {
      IsLoading = false;
      OnRootItemsChanged();
      OnPropertyChanged(nameof(ShowNoLogFilesMessage));
    }
  }

  partial void OnFilterTextChanged(string value)
  {
    ScheduleFilterDebounce();
  }

  partial void OnIsLoadingContentsChanged(bool value)
  {
    OnPropertyChanged(nameof(IsCopyButtonEnabled));
    OnPropertyChanged(nameof(IsDownloadButtonEnabled));
    OnPropertyChanged(nameof(IsRefreshContentsButtonEnabled));
    OnPropertyChanged(nameof(ShowSelectLogFilePrompt));
    OnPropertyChanged(nameof(ShowNoContentMessage));
    OnPropertyChanged(nameof(ShowNoFilterMatchesMessage));
    OnPropertyChanged(nameof(ShowLogContentTextBox));
  }

  partial void OnLogContentsChanged(string value)
  {
    OnPropertyChanged(nameof(IsCopyButtonEnabled));
    OnPropertyChanged(nameof(IsDownloadButtonEnabled));
    OnPropertyChanged(nameof(ShowNoContentMessage));
    RefreshDisplayedContents();
  }

  private void OnRootItemsChanged()
  {
    OnPropertyChanged(nameof(HasRootItems));
    OnPropertyChanged(nameof(ShowNoLogFilesMessage));
  }

  private void RefreshDisplayedContents()
  {
    _displayedLogContents = LogContentFilter.Apply(LogContents, FilterText);

    OnPropertyChanged(nameof(DisplayedLogContents));
    OnPropertyChanged(nameof(ShowNoFilterMatchesMessage));
    OnPropertyChanged(nameof(ShowNoContentMessage));
    OnPropertyChanged(nameof(ShowLogContentTextBox));
  }

  private void ScheduleFilterDebounce()
  {
    _filterDebounceCts?.Cancel();
    _filterDebounceCts?.Dispose();
    _filterDebounceCts = new CancellationTokenSource();
    var token = _filterDebounceCts.Token;

    _ = Dispatcher.UIThread.InvokeAsync(async () =>
    {
      try
      {
        await Task.Delay(_filterDebounceInterval, token);
      }
      catch (TaskCanceledException)
      {
        return;
      }

      if (token.IsCancellationRequested)
      {
        return;
      }

      RefreshDisplayedContents();
    });
  }

  private void SubscribeToTreeItem(LogFilesTreeItemViewModel item)
  {
    item.PropertyChanged += HandleTreeItemPropertyChanged;
  }

  private void UnsubscribeAllTreeItems()
  {
    UnsubscribeTreeItems(RootItems);
  }

  private void UnsubscribeTreeItems(IEnumerable<LogFilesTreeItemViewModel> items)
  {
    foreach (var item in items)
    {
      item.PropertyChanged -= HandleTreeItemPropertyChanged;
      UnsubscribeTreeItems(item.Children);
    }
  }
}
