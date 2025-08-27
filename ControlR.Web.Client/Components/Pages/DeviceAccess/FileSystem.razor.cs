using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class FileSystem : ComponentBase
{
  [Inject]
  public required IControlrApi ControlrApi { get; set; }

  [Inject]
  public required ISnackbar Snackbar { get; set; }

  [Inject]
  public required ILogger<FileSystem> Logger { get; set; }

  [Parameter]
  [SupplyParameterFromQuery]
  public Guid DeviceId { get; set; }

  public List<FileSystemTreeItemViewModel> TreeItems { get; set; } = [];
  public List<FileSystemEntryViewModel> DirectoryContents { get; set; } = [];
  
  private string? _selectedPath;
  public string? SelectedPath 
  { 
    get => _selectedPath;
    set
    {
      if (_selectedPath != value)
      {
        _selectedPath = value;
        InvokeAsync(async () => await OnSelectedPathChanged(value));
      }
    }
  }
  
  public bool IsLoading { get; set; }
  public bool IsLoadingContents { get; set; }

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

  private async Task LoadRootDrives()
  {
    try
    {
      IsLoading = true;
      StateHasChanged();

      var result = await ControlrApi.GetRootDrives(DeviceId);
      if (result.IsSuccess && result.Value is not null)
      {
        TreeItems = result.Value.Drives
          .Where(d => d.IsDirectory)
          .Select(ConvertToTreeItem)
          .ToList();

        // Select the first drive by default
        if (TreeItems.Count > 0)
        {
          SelectedPath = TreeItems[0].FullPath;
          await LoadDirectoryContents(SelectedPath);
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

  private async Task<HashSet<FileSystemTreeItemViewModel>> LoadServerData(FileSystemTreeItemViewModel parentNode)
  {
    try
    {
      if (!parentNode.IsDirectory || parentNode.HasLoadedChildren)
      {
        return parentNode.Children.ToHashSet();
      }

      parentNode.IsLoading = true;
      StateHasChanged();

      var result = await ControlrApi.GetSubdirectories(DeviceId, parentNode.FullPath);
      if (result.IsSuccess && result.Value is not null)
      {
        parentNode.Children = result.Value.Subdirectories
          .Select(ConvertToTreeItem)
          .ToList();
        
        parentNode.HasLoadedChildren = true;
      }
      else
      {
        Logger.LogWarning("Failed to load subdirectories for {Path}: {Error}", 
          parentNode.FullPath, result.Reason);
      }

      return parentNode.Children.ToHashSet();
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Exception while loading subdirectories for {Path}", parentNode.FullPath);
      return [];
    }
    finally
    {
      parentNode.IsLoading = false;
      StateHasChanged();
    }
  }

  private async Task OnNodeExpanded(FileSystemTreeItemViewModel node, bool expanded)
  {
    if (expanded && !node.HasLoadedChildren)
    {
      await LoadChildNodes(node);
    }
  }

  private async Task LoadChildNodes(FileSystemTreeItemViewModel parentNode)
  {
    try
    {
      if (!parentNode.IsDirectory || parentNode.HasLoadedChildren)
      {
        return;
      }

      parentNode.IsLoading = true;
      StateHasChanged();

      var result = await ControlrApi.GetSubdirectories(DeviceId, parentNode.FullPath);
      if (result.IsSuccess && result.Value is not null)
      {
        parentNode.Children = result.Value.Subdirectories
          .Select(ConvertToTreeItem)
          .ToList();
        
        parentNode.HasLoadedChildren = true;
      }
      else
      {
        Logger.LogWarning("Failed to load subdirectories for {Path}: {Error}", 
          parentNode.FullPath, result.Reason);
      }
    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Exception while loading subdirectories for {Path}", parentNode.FullPath);
    }
    finally
    {
      parentNode.IsLoading = false;
      StateHasChanged();
    }
  }

  private async Task LoadDirectoryContents(string directoryPath)
  {
    try
    {
      IsLoadingContents = true;
      StateHasChanged();

      var result = await ControlrApi.GetDirectoryContents(DeviceId, directoryPath);
      if (result.IsSuccess && result.Value is not null)
      {
        DirectoryContents = result.Value.Entries
          .Select(ConvertToViewModel)
          .OrderBy(x => !x.IsDirectory) // Directories first
          .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
          .ToList();
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
      StateHasChanged();
    }
  }

  private async Task OnSelectedPathChanged(string? newPath)
  {
    if (!string.IsNullOrEmpty(newPath) && newPath != SelectedPath)
    {
      SelectedPath = newPath;
      await LoadDirectoryContents(newPath);
    }
  }

  private static FileSystemTreeItemViewModel ConvertToTreeItem(FileSystemEntryDto dto)
  {
    return new FileSystemTreeItemViewModel
    {
      Name = dto.Name,
      FullPath = dto.FullPath,
      IsDirectory = dto.IsDirectory
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
}
