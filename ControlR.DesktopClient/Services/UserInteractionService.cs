using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Controls;
using ControlR.DesktopClient.Controls.Dialogs;
using Avalonia.Threading;

namespace ControlR.DesktopClient.Services;

public class UserInteractionService(IDialogProvider dialogProvider) : IUserInteractionService
{
  private readonly IDialogProvider _dialogProvider = dialogProvider;

  public async Task<bool> ShowConsentDialogAsync(string requesterName, CancellationToken cancellationToken)
  {
    // Ensure we are on the UI thread
    return await Dispatcher.UIThread.InvokeAsync(async () =>
    {
      var result = await _dialogProvider.ShowMessageBox(
        Localization.RemoteControlRequestTitle,
        string.Format(Localization.RemoteControlRequestMessage, requesterName),
        MessageBoxButtons.YesNo);

      return result == MessageBoxResult.Yes;
    });
  }
}
