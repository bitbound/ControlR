using Avalonia.Threading;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Views;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class Toaster(ILogger<Toaster> logger) : IToaster
{
  private readonly ILogger<Toaster> _logger = logger;

  public async Task ShowToast(string title, string message, ToastIcon toastIcon)
  {
    await ShowToast(title, message, toastIcon, () => Task.CompletedTask);
  }

  public async Task ShowToast(string title, string message, ToastIcon toastIcon, Func<Task> onClick)
  {
        try
    {
      // Ensure we're on the UI thread
      if (Dispatcher.UIThread.CheckAccess())
      {
        await ToastWindow.Show(title, message, toastIcon, onClick);
      }
      else
      {
        Dispatcher.UIThread.Post(async () => await ToastWindow.Show(title, message, toastIcon, onClick));
      }

      _logger.LogDebug("Toast notification shown: {Title} - {Message}", title, message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to show toast notification: {Title} - {Message}", title, message);
    }
  }

  public async Task ShowToast(string title, string message, ToastIcon toastIcon, Action onClick)
  {
    await ShowToast(title, message, toastIcon, () =>
    {
      onClick();
      return Task.CompletedTask;
    });
  }
}
