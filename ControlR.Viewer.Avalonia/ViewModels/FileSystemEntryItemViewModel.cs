using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Viewer.Avalonia.ViewModels;


public partial class FileSystemEntryItemViewModel : ObservableObject
{
  public required bool CanRead { get; init; }
  public required bool CanWrite { get; init; }
  public string EntryType => IsDirectory ? Assets.Resources.FileSystem_EntryTypeFolder : Assets.Resources.FileSystem_EntryTypeFile;
  public string FormattedSize => IsDirectory ? string.Empty : UnitsHelper.ToHumanReadableFileSize(Size);
  public required string FullPath { get; init; }
  public required bool IsDirectory { get; init; }
  public bool IsFile => !IsDirectory;
  public required bool IsHidden { get; init; }
  [ObservableProperty]
  public partial bool IsSelected { get; set; }
  public required DateTimeOffset LastModified { get; init; }
  public required string Name { get; init; }
  public required long Size { get; init; }

  public bool MatchesSearch(string searchText)
  {
    if (string.IsNullOrWhiteSpace(searchText))
    {
      return true;
    }

    return Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || FullPath.Contains(searchText, StringComparison.OrdinalIgnoreCase)
           || EntryType.Contains(searchText, StringComparison.OrdinalIgnoreCase);
  }
}