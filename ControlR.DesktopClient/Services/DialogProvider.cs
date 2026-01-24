using ControlR.DesktopClient.Controls.Dialogs;

namespace ControlR.DesktopClient.Services;

public interface IDialogProvider
{
  Task<MessageBoxResult> ShowMessageBox(string title, string message, MessageBoxButtons messageBoxButtons);
}

internal class DialogProvider : IDialogProvider
{
  public async Task<MessageBoxResult> ShowMessageBox(string title, string message, MessageBoxButtons messageBoxButtons)
  {
    return await MessageBox.Show(
      title,
      message,
      messageBoxButtons);
  }
}