using ControlR.DesktopClient.Controls;
using ControlR.DesktopClient.Controls.Dialogs;

namespace ControlR.DesktopClient.Services;

public interface IDialogProvider
{
  Task<MessageBoxResult> ShowMessageBox(string message, string caption, MessageBoxButtons messageBoxButtons);
}

internal class DialogProvider : IDialogProvider
{
  public async Task<MessageBoxResult> ShowMessageBox(string message, string caption, MessageBoxButtons messageBoxButtons)
  {
    return await MessageBox.Show(
      message,
      caption,
      messageBoxButtons);
  }
}