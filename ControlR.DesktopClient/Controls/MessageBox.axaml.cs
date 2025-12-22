using Avalonia.Controls;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Controls.Dialogs;

public partial class MessageBox : Window
{
  public MessageBox()
  {
    InitializeComponent();
  }

  public static async Task<MessageBoxResult> Show(string title, string message, MessageBoxButtons type)
  {
    return await Dispatcher.UIThread.InvokeAsync(async () =>
    {
      var viewModel = App.ServiceProvider.GetRequiredService<IMessageBoxViewModel>();
      var messageBox = new MessageBox()
      {
        DataContext = viewModel,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
      };
      viewModel.Title = title;
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

      if (App.MainWindow.IsVisible)
      {
        return await messageBox.ShowDialog<MessageBoxResult>(App.MainWindow);
      }
 
      return await messageBox.ShowHeadlessDialog<MessageBox, IMessageBoxViewModel, MessageBoxResult>(vm => vm.Result);
    });
  }
}