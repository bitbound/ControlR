using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class RemoteLogs : JsInteropableComponent
{
  private LogTreeNode? _selectedNode;

  [Inject]
  public required IClipboardManager ClipboardManager { get; init; }
  [Inject]
  public required IControlrApi ControlrApi { get; set; }
  [SupplyParameterFromQuery]
  public required Guid DeviceId { get; init; }
  [Inject]
  public required IDeviceState DeviceState { get; init; }
  [Inject]
  public required ILogger<RemoteLogs> Logger { get; set; }
  [Inject]
  public required ISnackbar Snackbar { get; set; }

  private bool IsCopyContentsButtonDisabled => string.IsNullOrEmpty(LogContents) || IsLoadingContents;
  private bool IsDownloadContentButtonDisabled => string.IsNullOrEmpty(LogContents) || IsLoadingContents;
  private bool IsLoading { get; set; }
  private bool IsLoadingContents { get; set; }
  private bool IsRefreshContentsButtonDisabled => _selectedNode is null || _selectedNode.IsFolder || IsLoadingContents;
  private string LogContents { get; set; } = string.Empty;
  private List<TreeItemData<LogTreeNode>> LogTreeItems { get; set; } = [];
  private LogTreeNode? SelectedNode
  {
    get => _selectedNode;
    set
    {
      if (_selectedNode == value)
      {
        return;
      }

      _selectedNode = value;
    }
  }

  protected override async Task OnInitializedAsync()
  {
    if (DeviceId == Guid.Empty)
    {
      Snackbar.Add("Device ID is required", Severity.Error);
      return;
    }

    await LoadLogFiles();
  }

  private async Task HandleCopyContentClicked()
  {
    try
    {
      await ClipboardManager.SetText(LogContents);
      Snackbar.Add("Log contents copied to clipboard", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error copying log contents to clipboard");
      Snackbar.Add("An error occurred while copying to clipboard", Severity.Error);
    }
  }

  private async Task HandleDownloadContentClicked()
  {
    try
    {
      if (SelectedNode is null || SelectedNode.IsFolder)
      {
        Snackbar.Add("No log file selected for download", Severity.Warning);
        return;
      }

      await WaitForJsModule();
      await JsModule.InvokeVoidAsync("downloadTextFile", SelectedNode.FileName, LogContents);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error downloading log contents");
      Snackbar.Add("An error occurred while downloading log contents", Severity.Error);
    }
  }

  private async Task HandleRefreshContentClicked()
  {
    await LoadLogContents();
  }

  private async Task LoadLogContents()
  {
    try
    {
      if (_selectedNode is null || _selectedNode.IsFolder)
      {
        LogContents = string.Empty;
        return;
      }

      IsLoadingContents = true;
      StateHasChanged();

      var result = await ControlrApi.GetLogFileContents(DeviceId, _selectedNode.Path!);

      if (!result.IsSuccess)
      {
        Logger.LogError("Failed to load log file contents: {Error}", result.Reason);
        Snackbar.Add($"Failed to load log file: {result.Reason}", Severity.Error);
        LogContents = string.Empty;
        return;
      }

      LogContents = result.Value;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error loading log file contents");
      Snackbar.Add("An error occurred while loading the log file", Severity.Error);
      LogContents = string.Empty;
    }
    finally
    {
      IsLoadingContents = false;
      StateHasChanged();
    }
  }

  private async Task LoadLogFiles()
  {
    try
    {
      IsLoading = true;
      StateHasChanged();

      var result = await ControlrApi.GetLogFiles(DeviceId);

      if (!result.IsSuccess || result.Value is null)
      {
        Logger.LogError("Failed to load log files: {Error}", result.Reason);
        Snackbar.Add($"Failed to load log files: {result.Reason}", Severity.Error);
        return;
      }

      var responseDto = result.Value;

      LogTreeItems = responseDto.LogFileGroups
        .Select(group => new TreeItemData<LogTreeNode>
        {
          Value = new LogTreeNode(true, null, null),
          Text = group.GroupName,
          Icon = Icons.Material.Filled.Folder,
          Expandable = true,
          Expanded = true,
          Children =
          [
            ..group.LogFiles.Select(file => new TreeItemData<LogTreeNode>
            {
              Value = new LogTreeNode(false, file.FullPath, file.FileName),
              Text = file.FileName,
              Icon = Icons.Material.Filled.Description,
              Expandable = false
            })
          ]
        })
        .ToList();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error loading log files");
      Snackbar.Add("An error occurred while loading log files", Severity.Error);
    }
    finally
    {
      IsLoading = false;
      StateHasChanged();
    }
  }

  private async Task OnRefreshTreeClick()
  {
    await LoadLogFiles();
  }

  private record LogTreeNode(bool IsFolder, string? Path, string? FileName)
  {
    public Guid Id { get; } = Guid.NewGuid();
  };
}
