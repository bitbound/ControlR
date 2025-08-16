using System;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Mac.Services;

public class ToasterMac(ILogger<ToasterMac> logger) : IToaster
{
  private readonly ILogger<ToasterMac> _logger = logger;
  public Task ShowToast(string title, string message, ToastIcon toastIcon)
  {
    try
    {
      // Create NSUserNotification
      var notification = AppKit.CreateNSUserNotification();

      // Set title and message
      AppKit.SetNotificationTitle(notification, title);
      AppKit.SetNotificationInformativeText(notification, message);

      // Set sound based on toast icon
      var soundName = GetSoundNameForIcon(toastIcon);
      if (!string.IsNullOrEmpty(soundName))
      {
        AppKit.SetNotificationSoundName(notification, soundName);
      }

      // Deliver the notification
      AppKit.DeliverNotification(notification);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to show toast notification: {Title} - {Message}", title, message);
    }
    return Task.CompletedTask;
  }

  private static string GetSoundNameForIcon(ToastIcon toastIcon)
  {
    return toastIcon switch
    {
      ToastIcon.Error => "Basso",           // macOS error sound
      ToastIcon.Warning => "Sosumi",        // macOS warning-like sound
      ToastIcon.Success => "Glass",         // macOS success-like sound
      ToastIcon.Info => "Ping",             // macOS info sound
      ToastIcon.Question => "Funk",         // macOS question-like sound
      _ => "Glass"                          // Default sound
    };
  }
}
