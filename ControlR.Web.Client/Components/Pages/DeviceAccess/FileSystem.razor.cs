using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Libraries.Shared.Services.Http;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public partial class FileSystem : ComponentBase
{

  private string? _selectedPath;
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
        InvokeAsync(async () => await OnSelectedPathChanged(value));
      }
    }
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
}
