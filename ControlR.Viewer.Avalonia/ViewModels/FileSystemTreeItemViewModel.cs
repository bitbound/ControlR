using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlR.Viewer.Avalonia.ViewModels;

public partial class FileSystemTreeItemViewModel : ObservableObject
{
  private bool _isExpanded;
  private bool _isSelected;

  public FileSystemTreeItemViewModel(string name, string fullPath, bool hasUnloadedChildren, bool isPlaceholder = false)
  {
    Name = name;
    FullPath = fullPath;
    HasUnloadedChildren = hasUnloadedChildren;
    IsPlaceholder = isPlaceholder;

    if (hasUnloadedChildren)
    {
      Children.Add(CreatePlaceholder());
    }
  }

  public ObservableCollection<FileSystemTreeItemViewModel> Children { get; } = [];
  public string FullPath { get; }
  public bool HasUnloadedChildren { get; private set; }
  public bool IsExpanded
  {
    get => _isExpanded;
    set => SetProperty(ref _isExpanded, value);
  }

  public bool IsPlaceholder { get; }
  public bool IsSelected
  {
    get => _isSelected;
    set => SetProperty(ref _isSelected, value);
  }

  public string Name { get; }

  public void ReplaceChildren(IEnumerable<FileSystemTreeItemViewModel> children)
  {
    Children.Clear();
    foreach (var child in children)
    {
      Children.Add(child);
    }

    HasUnloadedChildren = false;
  }

  public void ResetChildrenPlaceholder()
  {
    Children.Clear();
    if (HasUnloadedChildren)
    {
      Children.Add(CreatePlaceholder());
    }
  }

  public void SetExpandable(bool isExpandable)
  {
    HasUnloadedChildren = isExpandable;
    if (!isExpandable)
    {
      Children.Clear();
      return;
    }

    if (Children.Count == 0)
    {
      Children.Add(CreatePlaceholder());
    }
  }

  private static FileSystemTreeItemViewModel CreatePlaceholder()
  {
    return new FileSystemTreeItemViewModel(string.Empty, string.Empty, hasUnloadedChildren: false, isPlaceholder: true);
  }
}