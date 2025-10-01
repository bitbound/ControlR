using ControlR.Web.Client.Extensions;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class FileSystem : JsInteropableComponent
{
  private ElementReference _containerRef;
  private ElementReference _contentPanelRef;
  private InputFile _fileInputRef = default!;
  private string _searchText = string.Empty;

  private string? _selectedPath;
  private ElementReference _splitterRef;
  private ElementReference _treePanelRef;

  public string AddressBarValue { get; set; } = string.Empty;

  [Inject]
  public required IControlrApi ControlrApi { get; set; }

  public Guid DeviceId => DeviceState.IsDeviceLoaded
    ? DeviceState.CurrentDevice.Id
    : Guid.Empty;

  [Inject]
  public required IDeviceState DeviceState { get; init; }

  [Inject]
  public required IDialogService DialogService { get; set; }

  public List<FileSystemEntryViewModel> DirectoryContents { get; set; } = [];
  public List<TreeItemData<string>> InitialTreeItems { get; set; } = [];
  public bool IsDeleteInProgress { get; set; }
  public bool IsDownloadInProgress { get; set; }

  public bool IsLoading { get; set; }
  public bool IsLoadingContents { get; set; }
  public bool IsNewFolderInProgress { get; set; }
  public bool IsUploadInProgress { get; set; }

  [Inject]
  public required ILogger<FileSystem> Logger { get; set; }

  public HashSet<FileSystemEntryViewModel> SelectedItems { get; set; } = [];

  public string? SelectedPath
  {
    get => _selectedPath;
    set
    {
      if (string.IsNullOrWhiteSpace(value) ||
          _selectedPath == value)
      {
        return;
      }

      _selectedPath = value;
      AddressBarValue = value;
      InvokeAsync(async () => await OnSelectedPathChanged(value));
    }
  }

  [Inject]
  public required ISnackbar Snackbar { get; set; }

  public override async ValueTask DisposeAsync()
  {
    try
    {
      if (JsModuleReady.IsSet)
      {
        await JsModule.InvokeVoidAsync("dispose");
      }

      GC.SuppressFinalize(this);
    }
    catch (JSDisconnectedException)
    {
      // Ignore if the JS runtime is already disconnected
    }

    await base.DisposeAsync();
  }

  public async Task<IReadOnlyCollection<TreeItemData<string>>> LoadServerData(string? parentValue)
  {
    try
    {
      if (string.IsNullOrEmpty(parentValue))
      {
        return [];
      }

      var result = await ControlrApi.GetSubdirectories(DeviceId, parentValue);
      if (result.IsSuccess && result.Value is not null)
      {
        return [.. result.Value.Subdirectories.Select(ConvertToTreeItemData)];
      }

      Logger.LogWarning("Failed to load subdirectories for {Path}: {Error}",
        parentValue, result.Reason);
      return [];
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Exception while loading subdirectories for {Path}", parentValue);
      return [];
    }
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);

    if (firstRender)
    {
      await JsModuleReady.Wait(CancellationToken.None);
      await JsModule.InvokeVoidAsync("initializeGridSplitter", _containerRef, _splitterRef, _treePanelRef,
        _contentPanelRef);
    }
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    if (DeviceId == Guid.Empty)
    {
      Snackbar.Add("Device ID is required", Severity.Error);
      return;
    }

    await LoadRootDrives();
  }

  private static string CombinePaths(string path1, string path2, string pathSeparator)
  {
    return $"{path1.TrimEnd(pathSeparator.ToCharArray())}{pathSeparator}{path2.TrimStart(pathSeparator.ToCharArray())}";
  }

  private static TreeItemData<string> ConvertToTreeItemData(FileSystemEntryDto dto)
  {
    return new TreeItemData<string>
    {
      Value = dto.FullPath,
      Text = dto.Name,
      Icon = Icons.Material.Filled.Folder,
      Expandable = dto.IsDirectory && dto.HasSubfolders
    };
  }

  private static FileSystemEntryViewModel ConvertToViewModel(FileSystemEntryDto dto)
  {
    return new FileSystemEntryViewModel
    {
      Name = dto.Name,
      FullPath = dto.FullPath,
      IsDirectory = dto.IsDirectory,
      Size = dto.Size,
      LastModified = dto.LastModified,
      IsHidden = dto.IsHidden,
      CanRead = dto.CanRead,
      CanWrite = dto.CanWrite
    };
  }

  private async Task<bool> BuildTreeToPath(string targetPath)
  {
    try
    {
      // Get path segments from the agent to validate and parse the path correctly
      var pathSegmentsResult = await ControlrApi.GetPathSegments(DeviceId, targetPath);

      if (!pathSegmentsResult.IsSuccess || pathSegmentsResult.Value is null)
      {
        Logger.LogWarning("Failed to get path segments for {TargetPath}: {ErrorMessage}",
          targetPath, pathSegmentsResult.Reason);
        Snackbar.Add($"Error validating path '{targetPath}'", Severity.Error);
        return false;
      }

      var responseDto = pathSegmentsResult.Value;

      if (!responseDto.Success)
      {
        Logger.LogWarning("Path segments request failed for {TargetPath}: {Error}",
          targetPath, responseDto.ErrorMessage);
        Snackbar.Add($"Error validating path '{targetPath}': {responseDto.ErrorMessage}", Severity.Error);
        return false;
      }

      if (!responseDto.PathExists)
      {
        Logger.LogWarning("Path {TargetPath} does not exist", targetPath);
        Snackbar.Add($"Path '{targetPath}' not found", Severity.Warning);
        return false;
      }

      // Build the tree hierarchy using the path segments from the agent
      var segments = responseDto.PathSegments;
      if (segments.Length == 0)
      {
        Logger.LogWarning("No path segments returned for {TargetPath}", targetPath);
        Snackbar.Add($"Error validating path '{targetPath}'", Severity.Error);
        return false;
      }

      // Build the path progressively and ensure each level exists in the tree
      var currentPath = segments[0]; // Start with root
      var currentItems = InitialTreeItems;

      // Navigate through each segment, expanding the tree as needed
      for (var i = 1; i < segments.Length; i++)
      {
        var segmentToFind = segments[i];

        // Find the parent item that contains our next segment
        var parentItem = FindTreeItem(currentItems, currentPath);
        if (parentItem is null)
        {
          Logger.LogWarning("Could not find tree item for path segment {PathSegment}", currentPath);
          Snackbar.Add($"Error navigating to '{targetPath}'", Severity.Error);
          break;
        }

        parentItem.Expanded = true;

        // If children aren't loaded yet, load them
        if (parentItem.Children is not { Count: > 0 })
        {
          var children = await LoadServerData(parentItem.Value);
          parentItem.Children = [.. children];
        }

        currentPath = CombinePaths(currentPath, segmentToFind, responseDto.PathSeparator);
        currentItems = parentItem.Children;
      }

      StateHasChanged();
      return true;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error building tree to path {Path}", targetPath);
      Snackbar.Add($"Error navigating to '{targetPath}'", Severity.Error);
      return false;
    }
  }

  private EventCallback<IReadOnlyCollection<TreeItemData<string?>>?> CreateItemsChangedCallback(
    TreeItemData<string> treeItem)
  {
    return EventCallback.Factory.Create<IReadOnlyCollection<TreeItemData<string?>>?>(this, children =>
    {
      treeItem.Children = children
        ?.Where(x => x.Value is not null)
        .Cast<TreeItemData<string>>()
        .ToList();
    });
  }

  private async Task DeleteSingleItem(FileSystemEntryViewModel item)
  {
    try
    {
      var result = await ControlrApi.DeleteFile(DeviceId, item.FullPath, item.IsDirectory);

      if (!result.IsSuccess)
      {
        Logger.LogError("Delete failed for {ItemName}: {Error}", item.Name, result.Reason);
        throw new Exception($"Delete failed: {result.Reason}");
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error deleting item {ItemName}", item.Name);
      throw;
    }
  }

  private async Task DownloadSingleItem(FileSystemEntryViewModel item)
  {
    try
    {
      var downloadUrl =
        $"{HttpConstants.DeviceFileOperationsEndpoint}/download/{DeviceId}?filePath={Uri.EscapeDataString(item.FullPath)}&fileName={Uri.EscapeDataString(item.Name)}";

      await JsModule.InvokeVoidAsync("downloadFile", downloadUrl, item.Name);
      Snackbar.Add($"Started download of '{item.Name}'", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error downloading {ItemName}", item.Name);
      throw;
    }
  }

  private TreeItemData<string>? FindTreeItem(IEnumerable<TreeItemData<string>>? items, string path)
  {
    if (items == null)
    {
      return null;
    }

    return items.FirstOrDefault(item =>
      string.Equals(item.Value, path, StringComparison.OrdinalIgnoreCase));
  }

  private async Task HandleFileSystemRowClick(DataGridRowClickEventArgs<FileSystemEntryViewModel> args)
  {
    if (args.MouseEventArgs.CtrlKey || !args.Item.IsDirectory)
    {
      if (SelectedItems.Contains(args.Item))
      {
        SelectedItems.Remove(args.Item);
      }
      else
      {
        SelectedItems.Add(args.Item);
      }
    }
    else
    {
      AddressBarValue = args.Item.FullPath;
      await NavigateToAddress();
    }
  }

  private async Task LoadDirectoryContents(string directoryPath)
  {
    try
    {
      IsLoadingContents = true;
      await InvokeAsync(StateHasChanged);

      var result = await ControlrApi.GetDirectoryContents(DeviceId, directoryPath);
      if (result is { IsSuccess: true, Value: not null })
      {
        if (!result.Value.DirectoryExists)
        {
          Snackbar.Add($"Directory '{directoryPath}' was not found.", Severity.Warning);
          Logger.LogWarning("Directory does not exist: {Path}", directoryPath);
          DirectoryContents.Clear();
        }
        else
        {
          DirectoryContents = result.Value.Entries
            .Select(ConvertToViewModel)
            .OrderBy(x => !x.IsDirectory) // Directories first
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        }
      }
      else
      {
        Snackbar.Add($"Failed to load directory contents: {result.Reason}", Severity.Warning);
        Logger.LogWarning("Failed to load directory contents for {Path}: {Error}",
          directoryPath, result.Reason);
        DirectoryContents.Clear();
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add("An error occurred while loading directory contents", Severity.Error);
      Logger.LogError(ex, "Exception while loading directory contents for {Path}", directoryPath);
      DirectoryContents.Clear();
    }
    finally
    {
      IsLoadingContents = false;
      await InvokeAsync(StateHasChanged);
    }
  }

  private async Task LoadRootDrives()
  {
    try
    {
      IsLoading = true;
      StateHasChanged();

      var result = await ControlrApi.GetRootDrives(DeviceId);
      if (result is { IsSuccess: true, Value: not null })
      {
        InitialTreeItems = result.Value.Drives
          .Where(d => d.IsDirectory)
          .Select(ConvertToTreeItemData)
          .ToList();

        // Select the first drive by default
        if (InitialTreeItems.Count > 0)
        {
          AddressBarValue = SelectedPath ?? string.Empty;
          SelectedPath = InitialTreeItems[0].Value;
        }
      }
      else
      {
        Snackbar.Add($"Failed to load drives: {result.Reason}", Severity.Error);
        Logger.LogError("Failed to load root drives: {Error}", result.Reason);
      }
    }
    catch (Exception ex)
    {
      Snackbar.Add("An error occurred while loading drives", Severity.Error);
      Logger.LogError(ex, "Exception while loading root drives");
    }
    finally
    {
      IsLoading = false;
      StateHasChanged();
    }
  }

  private async Task NavigateToAddress()
  {
    var targetPath = AddressBarValue.Trim();

    if (string.IsNullOrWhiteSpace(targetPath))
    {
      return;
    }

    try
    {
      // Simply try to navigate to the path - let the agent validate it
      var buildResult = await BuildTreeToPath(targetPath);
      if (!buildResult)
      {
        return;
      }

      await InvokeAsync(StateHasChanged);
      await Task.Delay(100); // Small delay to ensure tree is updated

      // Set the selected path (this will also load directory contents)
      SelectedPath = targetPath;
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error navigating to path {Path}", targetPath);
      Snackbar.Add($"An error occurred while navigating to '{targetPath}'", Severity.Error);
    }
  }

  private async Task OnDeleteClick()
  {
    if (SelectedItems.Count == 0)
    {
      Snackbar.Add("Please select files or folders to delete", Severity.Warning);
      return;
    }

    var itemNames = string.Join(", ", SelectedItems.Select(x => x.Name));
    var message = SelectedItems.Count == 1
      ? $"Are you sure you want to delete '{SelectedItems.First().Name}'?"
      : $"Are you sure you want to delete {SelectedItems.Count} items? ({itemNames})";

    var result = await DialogService.ShowMessageBox(
      "Confirm Delete",
      message,
      "Delete",
      cancelText: "Cancel");

    if (result != true)
    {
      return;
    }

    IsDeleteInProgress = true;
    StateHasChanged();

    var deleteTasks = new List<Task>();

    foreach (var item in SelectedItems.ToList())
    {
      deleteTasks.Add(DeleteSingleItem(item));
    }

    try
    {
      await Task.WhenAll(deleteTasks);
      Snackbar.Add($"Successfully deleted {SelectedItems.Count} item(s)", Severity.Success);

      // Clear selection and refresh directory contents
      SelectedItems.Clear();
      await LoadDirectoryContents(SelectedPath!);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error during deletion");
      Snackbar.Add("One or more items failed to delete", Severity.Error);
    }
    finally
    {
      IsDeleteInProgress = false;
      StateHasChanged();
    }
  }

  private async Task OnDownloadClick()
  {
    if (SelectedItems.Count == 0)
    {
      Snackbar.Add("Please select files or folders to download", Severity.Warning);
      return;
    }

    IsDownloadInProgress = true;
    StateHasChanged();

    try
    {
      if (SelectedItems.Count == 1)
      {
        var item = SelectedItems.First();
        await DownloadSingleItem(item);
      }
      else
      {
        Snackbar.Add("Multiple file download not supported yet. Please select one item at a time.", Severity.Info);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error during download");
      Snackbar.Add("Download failed", Severity.Error);
    }
    finally
    {
      IsDownloadInProgress = false;
      StateHasChanged();
    }
  }

  private async Task OnFilesSelected(InputFileChangeEventArgs e)
  {
    if (string.IsNullOrEmpty(SelectedPath))
    {
      Snackbar.Add("Please select a directory first", Severity.Warning);
      return;
    }

    if (e.FileCount == 0)
    {
      return;
    }

    IsUploadInProgress = true;
    StateHasChanged();

    var uploadTasks = new List<Task>();

    foreach (var file in e.GetMultipleFiles()) // Limit to 10 files
    {
      uploadTasks.Add(UploadSingleFile(file));
    }

    try
    {
      await Task.WhenAll(uploadTasks);
      Snackbar.Add($"Successfully uploaded {e.FileCount} file(s)", Severity.Success);

      // Refresh directory contents
      await LoadDirectoryContents(SelectedPath);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error during file upload");
      Snackbar.Add("One or more files failed to upload", Severity.Error);
    }
    finally
    {
      IsUploadInProgress = false;
      StateHasChanged();
    }
  }

  private async Task OnNewFolderClick()
  {
    if (string.IsNullOrEmpty(SelectedPath))
    {
      Snackbar.Add("Please select a directory first", Severity.Warning);
      return;
    }

    try
    {
      var folderName = await DialogService.ShowPrompt(
        "Create New Folder",
        "Enter the name for the new folder:",
        "Folder Name",
        "Enter folder name here");

      if (string.IsNullOrWhiteSpace(folderName))
      {
        return; // User cancelled or provided empty name
      }

      IsNewFolderInProgress = true;
      StateHasChanged();

      // Create the directory using the new API structure
      var result = await ControlrApi.CreateDirectory(DeviceId, SelectedPath, folderName);
      if (result.IsSuccess)
      {
        Snackbar.Add($"Successfully created folder '{folderName}'", Severity.Success);
        await LoadDirectoryContents(SelectedPath);
      }
      else
      {
        Snackbar.Add($"Failed to create folder: {result.Reason}", Severity.Error);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error creating new folder");
      Snackbar.Add("An error occurred while creating the folder", Severity.Error);
    }
    finally
    {
      IsNewFolderInProgress = false;
      StateHasChanged();
    }
  }

  private async Task OnRefreshClick()
  {
    if (string.IsNullOrEmpty(SelectedPath))
    {
      await LoadRootDrives();
    }
    else
    {
      await LoadDirectoryContents(SelectedPath);
    }
  }

  private async Task OnSelectedPathChanged(string? newPath)
  {
    if (!string.IsNullOrEmpty(newPath))
    {
      SelectedItems.Clear();
      await LoadDirectoryContents(newPath);
    }
  }

  private async Task OnUploadFileClick()
  {
    try
    {
      await JsModule.InvokeVoidAsync("triggerFileInput", _fileInputRef.Element);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error triggering file input click");
      Snackbar.Add("Failed to open file picker", Severity.Error);
    }
  }

  private async Task UploadSingleFile(IBrowserFile file)
  {
    try
    {
      Guard.IsNotNull(SelectedPath);

      // Check if file already exists in current directory contents
      var existingFile = DirectoryContents.FirstOrDefault(f =>
        !f.IsDirectory && string.Equals(f.Name, file.Name, StringComparison.OrdinalIgnoreCase));

      if (existingFile != null)
      {
        // Show confirmation dialog for overwriting
        var confirmed = await DialogService.ShowMessageBox(
          "File Already Exists",
          $"The file '{file.Name}' already exists in the selected directory. Do you want to overwrite it?",
          "Overwrite",
          cancelText: "Cancel");

        if (confirmed != true)
        {
          // User cancelled, don't upload
          Logger.LogInformation("User cancelled overwrite for file {FileName}", file.Name);
          return;
        }
      }
      
      // TODO: Replace multipart form with raw stream and custom metadata header.
      await using var fileStream = file.OpenReadStream(100 * 1024 * 1024); // 100MB limit
      var result = await ControlrApi.UploadFile(DeviceId, SelectedPath, file.Name, fileStream, file.ContentType, true);

      if (!result.IsSuccess)
      {
        Logger.LogError("Upload failed for {FileName}: {Error}", file.Name, result.Reason);
        throw new Exception($"Upload failed: {result.Reason}");
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error uploading file {FileName}", file.Name);
      throw;
    }
  }
}