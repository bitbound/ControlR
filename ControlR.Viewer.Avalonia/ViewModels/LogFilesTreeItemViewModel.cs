using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.Viewer.Avalonia.ViewModels;

public partial class LogFilesTreeItemViewModel : ObservableObject
{
  private bool _isExpanded;
  private bool _isSelected;

  public LogFilesTreeItemViewModel(string name, string? fullPath, bool isFile)
  {
    Name = name;
    FullPath = fullPath;
    IsFile = isFile;
  }

  public ObservableCollection<LogFilesTreeItemViewModel> Children { get; } = [];

  public string? FullPath { get; }

  public bool IsExpanded
  {
    get => _isExpanded;
    set => SetProperty(ref _isExpanded, value);
  }

  public bool IsFile { get; }

  public bool IsSelected
  {
    get => _isSelected;
    set => SetProperty(ref _isSelected, value);
  }

  public string Name { get; }
}
