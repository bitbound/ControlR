using System.Threading.Channels;
using ControlR.Libraries.Shared.IO;
using ControlR.Web.Client.Components.FileSystem;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Client.Services.DeviceAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

// ReSharper disable AccessToDisposedClosure
namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class FileSystem : JsInteropableComponent
{
  private ElementReference _containerRef;
  private ElementReference _contentPanelRef;
  private InputFile _fileInputRef = null!;
  private string _searchText = string.Empty;

  private string? _selectedPath;
  private ElementReference _splitterRef;
  private ElementReference _treePanelRef;

  public string AddressBarValue { get; set; } = string.Empty;

  [Inject]
  public required IControlrApi ControlrApi { get; set; }

  [Inject]
  public required IDeviceState DeviceState { get; init; }

  [Inject]
  public required IDialogService DialogService { get; set; }

  public List<FileSystemEntryViewModel> DirectoryContents { get; set; } = [];

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

  [Inject]
  public required IHubConnection<IViewerHub> ViewerHub { get; set; }

  private Guid DeviceId => DeviceState.IsDeviceLoaded
    ? DeviceState.CurrentDevice.Id
    : Guid.Empty;

  private List<TreeItemData<string>> InitialTreeItems { get; set; } = [];
  private bool IsDeleteInProgress { get; set; }
  private bool IsLoading { get; set; }
  private bool IsLoadingContents { get; set; }
  private bool IsNewFolderInProgress { get; set; }
  private bool IsUpButtonDisabled => string.IsNullOrEmpty(SelectedPath) 
                                     || InitialTreeItems.Any(item => item.Value == SelectedPath);

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
      Expandable = dto is { IsDirectory: true, HasSubfolders: true }
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

  private static TreeItemData<string>? FindTreeItem(IEnumerable<TreeItemData<string>>? items, string path)
  {
    return items?.FirstOrDefault(item =>
      string.Equals(item.Value, path, StringComparison.OrdinalIgnoreCase));
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

  private async Task<Result<long>> GetMaxFileSize()
  {
    var getMaxSizeResult = await ControlrApi.GetFileUploadMaxSize();
    if (!getMaxSizeResult.IsSuccess)
    {
      Logger.LogError("Failed to get max upload size: {Error}", getMaxSizeResult.Reason);
      Snackbar.Add("Failed to determine max upload size. Upload cancelled.", Severity.Error);
      return Result.Fail<long>(getMaxSizeResult.Reason);
    }

    var maxFileSize = getMaxSizeResult.Value < 0
      ? long.MaxValue
      : getMaxSizeResult.Value;
    return Result.Ok(maxFileSize);
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

  private async Task<IReadOnlyCollection<TreeItemData<string>>> LoadServerData(string? parentValue)
  {
    try
    {
      if (string.IsNullOrEmpty(parentValue))
      {
        return [];
      }

      var result = await ControlrApi.GetSubdirectories(DeviceId, parentValue);
      if (result is { IsSuccess: true, Value: not null })
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
      await Task.Delay(100); // Small delay to ensure the tree is updated

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

    StateHasChanged();

    try
    {
      var getMaxSizeResult = await GetMaxFileSize();
      if (!getMaxSizeResult.IsSuccess)
      {
        return;
      }

      var maxFileSize = getMaxSizeResult.Value;

      var oversizedItems = SelectedItems
        .Where(item => !item.IsDirectory && item.Size > maxFileSize)
        .ToList();

      if (oversizedItems.Count > 0)
      {
        var itemNames = string.Join(", ", oversizedItems.Select(x => x.Name));
        var maxMb = (double)maxFileSize / (1024 * 1024);
        Snackbar.Add($"The following files exceed the maximum download size of {maxMb:N2} MB and cannot be downloaded: {itemNames}",
          Severity.Warning);
        Logger.LogWarning("Some selected files exceed max download size: {ItemNames}", itemNames);
        return;
      }

      foreach (var item in SelectedItems)
      {
        await DownloadSingleItem(item);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error during download");
      Snackbar.Add("Download failed", Severity.Error);
    }
    finally
    {
      StateHasChanged();
    }
  }

  private async Task OnFilesSelected(InputFileChangeEventArgs e)
  {
    try
    {
      StateHasChanged();

      if (string.IsNullOrEmpty(SelectedPath))
      {
        Snackbar.Add("Please select a directory first", Severity.Warning);
        return;
      }

      if (e.FileCount == 0)
      {
        return;
      }

      var uploadTasks = new List<Task>();
      foreach (var file in e.GetMultipleFiles())
      {
        var uploadTask = UploadSingleFile(file);
        uploadTasks.Add(uploadTask);
      }
      StateHasChanged();

      await Task.WhenAll(uploadTasks);

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
        return; // User canceled or provided an empty name
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

  private async Task OnUpOneLevel()
  {
    if (string.IsNullOrEmpty(SelectedPath))
    {
      return;
    }

    try
    {
      // Use the agent to get path segments, which will properly handle the parent path
      var pathSegmentsResult = await ControlrApi.GetPathSegments(DeviceId, SelectedPath);

      if (!pathSegmentsResult.IsSuccess || pathSegmentsResult.Value is null)
      {
        Logger.LogWarning("Failed to get path segments for {SelectedPath}: {ErrorMessage}",
          SelectedPath, pathSegmentsResult.Reason);
        Snackbar.Add("Unable to navigate up one level", Severity.Warning);
        return;
      }

      var responseDto = pathSegmentsResult.Value;

      if (!responseDto.Success)
      {
        Logger.LogWarning("Path segments request failed for {SelectedPath}: {Error}",
          SelectedPath, responseDto.ErrorMessage);
        Snackbar.Add($"Error navigating up: {responseDto.ErrorMessage}", Severity.Warning);
        return;
      }

      // Get the parent path by removing the last segment
      var segments = responseDto.PathSegments;
      if (segments.Length <= 1)
      {
        // Already at root level
        return;
      }

      // Build the parent path from all segments except the last one
      var parentPath = segments[0];
      for (var i = 1; i < segments.Length - 1; i++)
      {
        parentPath = CombinePaths(parentPath, segments[i], responseDto.PathSeparator);
      }

      // Build the tree to the parent path and navigate
      var buildResult = await BuildTreeToPath(parentPath);
      if (buildResult)
      {
        SelectedPath = parentPath;
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error navigating up one level from {Path}", SelectedPath);
      Snackbar.Add("An error occurred while navigating up one level", Severity.Error);
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
    var snackbarKey = Guid.NewGuid().ToString();
    try
    {
      Guard.IsNotNull(SelectedPath);

      // Check if the file already exists in current directory contents
      var existingFile = DirectoryContents.FirstOrDefault(f =>
        !f.IsDirectory && string.Equals(f.Name, file.Name, StringComparison.OrdinalIgnoreCase));

      if (existingFile != null)
      {
        // Show the confirmation dialog for overwriting
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

      var getMaxSizeResult = await GetMaxFileSize();
      if (!getMaxSizeResult.IsSuccess)
      {
        return;
      }

      var maxFileSize = getMaxSizeResult.Value;

      if (file.Size > maxFileSize)
      {
        var maxMb = (double)maxFileSize / (1024 * 1024);
        Snackbar.Add($"File '{file.Name}' exceeds the maximum upload size of {maxMb:N2} MB.",
          Severity.Warning);
        Logger.LogWarning("File {FileName} size {FileSize} exceeds max upload size {MaxSize}",
          file.Name, file.Size, maxFileSize);
        return;
      }

      using var cts = new CancellationTokenSource();
      await using var fileStream = file.OpenReadStream(maxFileSize, cts.Token);
      await using var observer = new StreamObserver(
        observedStream: fileStream,
        observationInterval: TimeSpan.FromMilliseconds(500));

      using var snackbar = Snackbar.Add<FileUploadIndicator>(
        new Dictionary<string, object>()
        {
          {"File", file },
          {"StreamObserver", observer }
        },
        Severity.Normal,
        config =>
        {
          config.Icon = Icons.Material.Filled.UploadFile;
          config.RequireInteraction = true;
          config.VisibleStateDuration = int.MaxValue;
          config.ShowCloseIcon = false;
          config.Action = "Cancel";
          config.ActionColor = Color.Error;
          config.ActionVariant = Variant.Outlined;
          config.OnClick = _ =>
          {
            cts.Cancel();
            return Task.CompletedTask;
          };
        },
        key: snackbarKey);

      if (snackbar is not null)
      {
        snackbar.OnClose += _ =>
        {
          cts.Cancel();
        };
      }

      var metadata = new FileUploadMetadata(
        DeviceId,
        SelectedPath,
        file.Name,
        file.Size,
        file.ContentType,
        true);

      var channel = Channel.CreateBounded<byte[]>(10);

      // Write file chunks to channel in the background
      var writeTask = Task.Run(async () =>
      {
        try
        {
          var buffer = new byte[AppConstants.SignalrMaxMessageSize];
          int bytesRead;

          while ((bytesRead = await fileStream.ReadAsync(buffer, cts.Token)) > 0)
          {
            var chunk = buffer[..bytesRead];
            await channel.Writer.WriteAsync(chunk, cts.Token);
          }
          channel.Writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
          channel.Writer.TryComplete(new OperationCanceledException("Upload was canceled."));
        }
        catch (Exception ex)
        {
          channel.Writer.TryComplete(ex);
          Logger.LogError(ex, "Error writing file chunks to channel for {FileName}", file.Name);
        }
      }, cts.Token);

      var result = await ViewerHub.Server
        .UploadFile(metadata, channel.Reader)
        .WaitAsync(cts.Token);

      // Wait for the write task to complete
      try
      {
        await writeTask;
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error in background write task for {FileName}", file.Name);
      }

      if (result.IsSuccess)
      {
        Logger.LogInformation("Successfully uploaded file {FileName}", file.Name);
        Snackbar.Add($"Successfully uploaded '{file.Name}'", Severity.Success);
      }
      else
      {
        if (cts.IsCancellationRequested)
        {
          Logger.LogInformation("Upload cancelled for file {FileName}", file.Name);
          Snackbar.Add($"Upload cancelled for '{file.Name}'", Severity.Info);
          return;
        }
        Logger.LogError("Upload failed for {FileName}: {Error}", file.Name, result.Reason);
        Snackbar.Add($"Upload failed for '{file.Name}': {result.Reason}", Severity.Error);
      }
    }
    catch (OperationCanceledException)
    {
      Logger.LogInformation("Upload operation cancelled for file {FileName}", file.Name);
      Snackbar.Add($"Upload cancelled for '{file.Name}'", Severity.Info);
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error uploading file {FileName}", file.Name);
      Snackbar.Add($"An error occurred while uploading '{file.Name}'", Severity.Error);
    }
    finally
    {
      // Remove the snackbar if it still exists
      Snackbar.RemoveByKey(snackbarKey);
      await InvokeAsync(StateHasChanged);
    }
  }
}
