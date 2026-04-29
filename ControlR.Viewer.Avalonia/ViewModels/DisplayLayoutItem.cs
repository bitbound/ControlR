using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Viewer.Avalonia.ViewModels;

public sealed partial class DisplayLayoutItem : ObservableObject
{
  private readonly Func<DisplayLayoutItem, Task> _selectHandler;

  private bool _isSelected;
  private double _layoutHeight;
  private double _layoutLeft;
  private double _layoutTop;
  private double _layoutWidth;

  public DisplayLayoutItem(DisplayDto display, Func<DisplayLayoutItem, Task> selectHandler)
  {
    Display = display;
    _selectHandler = selectHandler;
    SelectCommand = new AsyncRelayCommand(Select);
  }

  public DisplayDto Display { get; }
  public string DisplayId => Display.DisplayId;
  public int Index => Display.Index + 1;
  public bool IsSelected
  {
    get => _isSelected;
    set => SetProperty(ref _isSelected, value);
  }

  public double LayoutHeight
  {
    get => _layoutHeight;
    set => SetProperty(ref _layoutHeight, value);
  }

  public double LayoutLeft
  {
    get => _layoutLeft;
    set => SetProperty(ref _layoutLeft, value);
  }

  public double LayoutTop
  {
    get => _layoutTop;
    set => SetProperty(ref _layoutTop, value);
  }

  public double LayoutWidth
  {
    get => _layoutWidth;
    set => SetProperty(ref _layoutWidth, value);
  }

  public string Name => Display.Name;
  public IAsyncRelayCommand SelectCommand { get; }

  private Task Select()
  {
    return _selectHandler.Invoke(this);
  }
}
