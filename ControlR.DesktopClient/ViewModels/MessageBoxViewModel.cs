using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.DesktopClient.Controls;
using System.Windows.Input;

namespace ControlR.DesktopClient.ViewModels;

public interface IMessageBoxViewModel : IViewModelBase
{
  bool AreYesNoButtonsVisible { get; set; }
  bool IsOkButtonVisible { get; set; }
  string? Message { get; set; }
  ICommand NoCommand { get; }
  ICommand OKCommand { get; }
  MessageBoxResult Result { get; }
  string? Title { get; set; }
  ICommand YesCommand { get; }
}

public partial class MessageBoxViewModel : ViewModelBase, IMessageBoxViewModel
{
  [ObservableProperty]
  private bool _areYesNoButtonsVisible;
  [ObservableProperty]
  private bool _isOkButtonVisible;
  [ObservableProperty]
  private string? _message;
  [ObservableProperty]
  private string? _title;

  public MessageBoxViewModel()
  {
    if (Design.IsDesignMode)
    {
      Message = "This is a design-time message.";
      Title = "Design Time Caption";
      AreYesNoButtonsVisible = true;
    }
  }

  public ICommand NoCommand => new RelayCommand<Window>(window =>
  {
    Result = MessageBoxResult.No;
    window?.Close(MessageBoxResult.No);
  });
  public ICommand OKCommand => new RelayCommand<Window>(window =>
  {
    Result = MessageBoxResult.OK;
    window?.Close(MessageBoxResult.OK);
  });
  public MessageBoxResult Result { get; private set; }
  public ICommand YesCommand => new RelayCommand<Window>(window =>
  {
    Result = MessageBoxResult.Yes;
    window?.Close(MessageBoxResult.Yes);
  });
}