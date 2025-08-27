using ControlR.Web.Client.Extensions;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class FileSystem : JsInteropableComponent
{
  private ElementReference _containerRef;
  private ElementReference _splitterRef;
  private ElementReference _treePanelRef;
  private ElementReference _contentPanelRef;
  private InputFile _fileInputRef = default!;

  private string? _selectedPath;
  private string _addressBarValue = string.Empty;
  
  [Inject]
  public required IControlrApi ControlrApi { get; set; }

  [Inject]
  public required IDialogService DialogService { get; set; }

  [Inject]
  public required IDeviceState DeviceState { get; init; }

  public Guid DeviceId => DeviceState.IsDeviceLoaded
    ? DeviceState.CurrentDevice.Id 
    : Guid.Empty;
  
  public List<FileSystemEntryViewModel> DirectoryContents { get; set; } = [];
  public HashSet<FileSystemEntryViewModel> SelectedItems { get; set; } = [];
  public List<TreeItemData<string>> InitialTreeItems { get; set; } = [];

  public bool IsLoading { get; set; }
  public bool IsLoadingContents { get; set; }
  public bool IsUploadInProgress { get; set; }
  public bool IsDownloadInProgress { get; set; }
  public bool IsDeleteInProgress { get; set; }
  public bool IsNewFolderInProgress { get; set; }

  [Inject]
  public required ILogger<FileSystem> Logger { get; set; }
  public string? SelectedPath
  {
    get => _selectedPath;
    set
    {
      if (_selectedPath != value)
      {
        _selectedPath = value;
        _addressBarValue = value ?? string.Empty;
        InvokeAsync(async () => await OnSelectedPathChanged(value));
      }
    }
  }

  public string AddressBarValue
  {
    get => _addressBarValue;
    set => _addressBarValue = value;
  }

  [Inject]
  public required ISnackbar Snackbar { get; set; }

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
        return result.Value.Subdirectories
          .Select(ConvertToTreeItemData)
          .ToList();
      }
      else
      {
        Logger.LogWarning("Failed to load subdirectories for {Path}: {Error}",
          parentValue, result.Reason);
        return [];
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Exception while loading subdirectories for {Path}", parentValue);
      return [];
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

  protected override async Task OnParametersSetAsync()
  {
    await base.OnParametersSetAsync();
    if (DeviceId != Guid.Empty && !IsLoading)
    {
      await LoadRootDrives();
    }
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

  private async Task LoadDirectoryContents(string directoryPath)
  {
    try
    {
      IsLoadingContents = true;
      await InvokeAsync(StateHasChanged);

      var result = await ControlrApi.GetDirectoryContents(DeviceId, directoryPath);
      if (result.IsSuccess && result.Value is not null)
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
      if (result.IsSuccess && result.Value is not null)
      {
        InitialTreeItems = result.Value.Drives
          .Where(d => d.IsDirectory)
          .Select(ConvertToTreeItemData)
          .ToList();

        // Select the first drive by default
        if (InitialTreeItems.Count > 0)
        {
          SelectedPath = InitialTreeItems[0].Value;
          _addressBarValue = SelectedPath ?? string.Empty;
          if (!string.IsNullOrEmpty(SelectedPath))
          {
            await LoadDirectoryContents(SelectedPath);
          }
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

  private void OnItemsLoaded(TreeItemData<string> treeItemData, IReadOnlyCollection<TreeItemData<string>> children)
  {
    treeItemData.Children = children?.ToList();
  }

  private async Task OnSelectedPathChanged(string? newPath)
  {
    if (!string.IsNullOrEmpty(newPath))
    {
      SelectedItems.Clear();
      await LoadDirectoryContents(newPath);
    }
  }

  private async Task NavigateToAddress()
  {
    var targetPath = _addressBarValue?.Trim();
    if (string.IsNullOrWhiteSpace(targetPath))
    {
      return;
    }

    try
    {
      // First, try to get directory contents to validate the path
      var result = await ControlrApi.GetDirectoryContents(DeviceId, targetPath);
      if (!result.IsSuccess || result.Value is null)
      {
        Snackbar.Add($"Could not navigate to '{targetPath}': {result.Reason}", Severity.Warning);
        return;
      }

      // Check if the directory actually exists
      if (!result.Value.DirectoryExists)
      {
        Snackbar.Add($"Directory '{targetPath}' was not found.", Severity.Warning);
        return;
      }

      // Path is valid, now build the tree structure to this path
      await BuildTreeToPath(targetPath);
      
      await InvokeAsync(StateHasChanged);
      await Task.Delay(100); // Small delay to ensure tree is updated

      // Set the selected path (this will also load directory contents)
      SelectedPath = targetPath;
      
      Snackbar.Add($"Navigated to '{targetPath}'", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error navigating to path {Path}", targetPath);
      Snackbar.Add($"An error occurred while navigating to '{targetPath}'", Severity.Error);
    }
  }

  private async Task BuildTreeToPath(string targetPath)
  {
    try
    {
      // Parse the path into segments
      var pathSegments = GetPathSegments(targetPath);
      if (pathSegments.Count == 0)
      {
        return;
      }

      // Find or load the root drive
      var rootPath = pathSegments[0];
      var rootItem = InitialTreeItems.FirstOrDefault(x =>
        string.Equals(x.Value, rootPath, StringComparison.OrdinalIgnoreCase));

      if (rootItem is null)
      {
        // Root drive not found, reload root drives
        await LoadRootDrives();
        rootItem = InitialTreeItems.FirstOrDefault(x =>
          string.Equals(x.Value, rootPath, StringComparison.OrdinalIgnoreCase));

        if (rootItem is null)
        {
          Logger.LogWarning("Root path {RootPath} not found in drives", rootPath);
          return;
        }
      }

      // Build the tree structure down to the target path
      var currentItem = rootItem;
      var currentPath = rootPath;

      for (int i = 1; i < pathSegments.Count; i++)
      {
        var nextSegment = pathSegments[i];
        var nextPath = Path.Combine(currentPath, nextSegment);

        // Ensure current item has its children loaded
        if (currentItem.Children is null || currentItem.Children.Count == 0)
        {
          var children = await LoadServerData(currentItem.Value);
          currentItem.Children = [.. children];
        }

        // Find the next item in the children
        var nextItem = currentItem.Children?.FirstOrDefault(x =>
          string.Equals(x.Value, nextPath, StringComparison.OrdinalIgnoreCase));

        if (nextItem is null)
        {
          Logger.LogWarning("Path segment {NextPath} not found in children of {CurrentPath}", nextPath, currentPath);
          break;
        }

        // Expand the current item and move to the next
        currentItem.Expanded = true;
        currentItem = nextItem;
        currentPath = nextPath;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error building tree to path {Path}", targetPath);
      Snackbar.Add($"Error navigating to '{targetPath}'", Severity.Error);
    }
  }

  private static List<string> GetPathSegments(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return [];
    }

    // Normalize path separators
    path = path.Replace('/', Path.DirectorySeparatorChar);
    
    // Handle different path formats
    if (Path.IsPathRooted(path))
    {
      var segments = new List<string>();
      
      // Add the root (drive or leading separator)
      var root = Path.GetPathRoot(path);
      if (!string.IsNullOrEmpty(root))
      {
        segments.Add(root);
        
        // Get the relative path after the root
        var relativePath = path.Substring(root.Length);
        if (!string.IsNullOrEmpty(relativePath))
        {
          segments.AddRange(relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
        }
      }
      
      return segments;
    }
    else
    {
      // Relative path - split by directory separator
      return path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    await base.OnAfterRenderAsync(firstRender);
    
    if (firstRender)
    {
      await JsModuleReady.Wait(CancellationToken.None);
      await JsModule.InvokeVoidAsync("initializeGridSplitter", _containerRef, _splitterRef, _treePanelRef, _contentPanelRef);
    }
  }

  public override async ValueTask DisposeAsync()
  {
    try
    {
      if (JsModuleReady.IsSet)
      {
        await JsModule.InvokeVoidAsync("dispose");
      }
    }
    catch (JSDisconnectedException)
    {
      // Ignore if the JS runtime is already disconnected
    }
    
    await base.DisposeAsync();
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
    
    foreach (var file in e.GetMultipleFiles(10)) // Limit to 10 files
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

  private async Task UploadSingleFile(IBrowserFile file)
  {
    try
    {
      Guard.IsNotNull(SelectedPath);

      // Check if file already exists in current directory contents
      var existingFile = DirectoryContents?.FirstOrDefault(f => 
        !f.IsDirectory && string.Equals(f.Name, file.Name, StringComparison.OrdinalIgnoreCase));

      if (existingFile != null)
      {
        // Show confirmation dialog for overwriting
        var confirmed = await DialogService.ShowMessageBox(
          "File Already Exists",
          $"The file '{file.Name}' already exists in the selected directory. Do you want to overwrite it?",
          yesText: "Overwrite",
          cancelText: "Cancel");

        if (confirmed != true)
        {
          // User cancelled, don't upload
          Logger.LogInformation("User cancelled overwrite for file {FileName}", file.Name);
          return;
        }
      
      }

      using var fileStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB limit
      var result = await ControlrApi.UploadFile(DeviceId, SelectedPath, file.Name, fileStream, file.ContentType, overwrite: true);
      
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

  private async Task DownloadSingleItem(FileSystemEntryViewModel item)
  {
    try
    {
      var downloadUrl = $"{HttpConstants.DeviceFileOperationsEndpoint}/download/{DeviceId}?filePath={Uri.EscapeDataString(item.FullPath)}";

      await JsModule.InvokeVoidAsync("downloadFile", downloadUrl, item.Name);
      Snackbar.Add($"Started download of '{item.Name}'", Severity.Success);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error downloading {ItemName}", item.Name);
      throw;
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
      yesText: "Delete",
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

      // Validate folder name
      var invalidChars = Path.GetInvalidFileNameChars();
      if (folderName.Any(c => invalidChars.Contains(c)))
      {
        var invalidCharList = string.Join(", ", folderName.Where(c => invalidChars.Contains(c)).Distinct());
        Snackbar.Add($"Folder name contains invalid characters: {invalidCharList}", Severity.Warning);
        return;
      }

      var fullPath = Path.Combine(SelectedPath, folderName);
      
      // Check if directory already exists by checking current directory contents
      if (DirectoryContents.Any(item => item.IsDirectory && 
          string.Equals(item.Name, folderName, StringComparison.OrdinalIgnoreCase)))
      {
        Snackbar.Add("A folder with that name already exists", Severity.Warning);
        return;
      }

      IsNewFolderInProgress = true;
      StateHasChanged();

      // Create the directory using the API
      var result = await ControlrApi.CreateDirectory(DeviceId, fullPath);
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
}
