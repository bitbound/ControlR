using Avalonia.Threading;
using ControlR.DesktopClient.Views;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

public class Toaster(ILogger<Toaster> logger) : IToaster
{
  private readonly ILogger<Toaster> _logger = logger;

  public async Task ShowToast(string title, string message, ToastIcon toastIcon, TimeSpan? closeAfter = null)
  {
    await ShowToastImpl(title, message, toastIcon, null, closeAfter);
  }

  public async Task ShowToast(string title, string message, ToastIcon toastIcon, Func<Task> onClick, TimeSpan? closeAfter = null)
  {
    await ShowToastImpl(title, message, toastIcon, onClick, closeAfter);
  }

  public async Task ShowToast(string title, string message, ToastIcon toastIcon, Action onClick, TimeSpan? closeAfter = null)
  {
    await ShowToastImpl(title, message, toastIcon, () =>
    {
      onClick();
      return Task.CompletedTask;
    }, closeAfter);
  }

  private async Task ShowToastImpl(string title, string message, ToastIcon toastIcon, Func<Task>? onClick = null, TimeSpan? closeAfter = null)
  {
    try
    {
      var timeout = closeAfter ?? TimeSpan.FromSeconds(10);
      // Ensure we're on the UI thread
      if (Dispatcher.UIThread.CheckAccess())
      {
        await ToastWindow.Show(title, message, toastIcon, onClick, timeout);
      }
      else
      {
        Dispatcher.UIThread.Post(async () => await ToastWindow.Show(title, message, toastIcon, onClick, timeout));
      }

      _logger.LogDebug("Toast notification shown: {Title} - {Message}", title, message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to show toast notification: {Title} - {Message}", title, message);
    }
  }
}
