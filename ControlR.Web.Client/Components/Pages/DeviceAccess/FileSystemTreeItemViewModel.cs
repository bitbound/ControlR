namespace ControlR.Web.Client.Components.Pages.DeviceAccess;

public class FileSystemTreeItemViewModel
{
  public List<FileSystemTreeItemViewModel> Children { get; set; } = [];
  public required string FullPath { get; set; }
  
  // Placeholder to show expand arrow even when children aren't loaded yet
  public bool HasChildren => IsDirectory && (!HasLoadedChildren || Children.Count > 0);
  public bool HasLoadedChildren { get; set; }
  public string Icon => Icons.Material.Filled.Folder;
  public required bool IsDirectory { get; set; }
  public bool IsExpanded { get; set; }
  public bool IsLoading { get; set; }
  public required string Name { get; set; }
}
