using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Controls;
using System.Windows.Input;

namespace ControlR.DesktopClient.ViewModels;

public interface IMessageBoxViewModel : IViewModelBase
{
  bool AreYesNoButtonsVisible { get; set; }
  string? Caption { get; set; }
  bool IsOkButtonVisible { get; set; }
  string? Message { get; set; }
  ICommand NoCommand { get; }
  ICommand OKCommand { get; }
  ICommand YesCommand { get; }
}

public partial class MessageBoxViewModel : ViewModelBase, IMessageBoxViewModel
{
  [ObservableProperty]
  private bool _areYesNoButtonsVisible;

  [ObservableProperty]
  private string? _caption;

  [ObservableProperty]
  private bool _isOkButtonVisible;

  [ObservableProperty]
  private string? _message;

  public MessageBoxViewModel()
  {
    if (Design.IsDesignMode)
    {
      Message = "This is a design-time message.";
      Caption = "Design Time Caption";
      AreYesNoButtonsVisible = true;
    }
  }

  public ICommand NoCommand => new RelayCommand<Window>(window =>
  {
    window?.Close(MessageBoxResult.No);
  });

  public ICommand OKCommand => new RelayCommand<Window>(window =>
  {
    window?.Close();
  });

  public ICommand YesCommand => new RelayCommand<Window>(window =>
  {
    window?.Close();
  });
#pragma warning restore S2325 // Methods and properties that don't access instance data should be static
}