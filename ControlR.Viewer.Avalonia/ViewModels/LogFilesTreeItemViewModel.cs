using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.Viewer.Avalonia.ViewModels;

public partial class LogFilesTreeItemViewModel : ObservableObject
{
  private bool _isExpanded;
  private bool _isSelected;

  public LogFilesTreeItemViewModel(string name, LogKind? kind, string? username, bool isFile)
  {
    Name = name;
    Kind = kind;
    Username = username;
    IsFile = isFile;
  }

  public ObservableCollection<LogFilesTreeItemViewModel> Children { get; } = [];
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
  public LogKind? Kind { get; }
  public string Name { get; }
  public string? Username { get; }
}
