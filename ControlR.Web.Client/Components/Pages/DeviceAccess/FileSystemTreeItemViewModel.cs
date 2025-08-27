using MudBlazor;

namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public class FileSystemTreeItemViewModel
{
  public required string Name { get; set; }
  public required string FullPath { get; set; }
  public required bool IsDirectory { get; set; }
  public string Icon => Icons.Material.Filled.Folder;
  public bool IsExpanded { get; set; }
  public bool IsLoading { get; set; }
  public bool HasLoadedChildren { get; set; }
  public List<FileSystemTreeItemViewModel> Children { get; set; } = [];
  
  // Placeholder to show expand arrow even when children aren't loaded yet
  public bool HasChildren => IsDirectory && (!HasLoadedChildren || Children.Count > 0);
}
