using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Web.Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class FileSystem : JsInteropableComponent
{
  private ElementReference _containerRef;
  private ElementReference _splitterRef;
  private ElementReference _treePanelRef;
  private ElementReference _contentPanelRef;

  private string? _selectedPath;
  private string _addressBarValue = string.Empty;
  
  [Inject]
  public required IControlrApi ControlrApi { get; set; }

  [Parameter]
  [SupplyParameterFromQuery]
  public Guid DeviceId { get; set; }
  public List<FileSystemEntryViewModel> DirectoryContents { get; set; } = [];

  public List<TreeItemData<string>> InitialTreeItems { get; set; } = [];

  public bool IsLoading { get; set; }
  public bool IsLoadingContents { get; set; }

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
    if (DeviceId == Guid.Empty)
    {
      Snackbar.Add("Device ID is required", Severity.Error);
      return;
    }

    await LoadRootDrives();
  }

  protected override async Task OnParametersSetAsync()
  {
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
      SelectedPath = newPath;
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
}
