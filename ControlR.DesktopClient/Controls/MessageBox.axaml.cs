using Avalonia.Controls;
using Avalonia.Threading;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Controls.Dialogs;

public partial class MessageBox : Window
{
  public MessageBox()
  {
    InitializeComponent();
  }

  public static async Task<MessageBoxResult> Show(string message, string caption, MessageBoxButtons type)
  {
    return await Dispatcher.UIThread.InvokeAsync(async () =>
    {
      var viewModel = App.ServiceProvider.GetRequiredService<IMessageBoxViewModel>();
      var messageBox = new MessageBox()
      {
        DataContext = viewModel,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
      };
      viewModel.Caption = caption;
      viewModel.Message = message;

      switch (type)
      {
        case MessageBoxButtons.OK:
          viewModel.IsOkButtonVisible = true;
          break;
        case MessageBoxButtons.YesNo:
          viewModel.AreYesNoButtonsVisible = true;
          break;
        default:
          break;
      }
      return await messageBox.ShowDialog<MessageBoxResult>(App.MainWindow);
    });
  }
}