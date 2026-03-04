using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

namespace ControlR.Viewer.Avalonia.ViewModels;

public sealed partial class DisplayLayoutItem : ObservableObject
{
  private readonly Func<DisplayLayoutItem, Task> _selectHandler;

  [ObservableProperty]
  private bool _isSelected;
  [ObservableProperty]
  private double _layoutHeight;
  [ObservableProperty]
  private double _layoutLeft;
  [ObservableProperty]
  private double _layoutTop;
  [ObservableProperty]
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
  public string Name => Display.Name;
  public IAsyncRelayCommand SelectCommand { get; }

  private Task Select()
  {
    return _selectHandler.Invoke(this);
  }
}
