using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace ControlR.DesktopClient.Windows.Services;

internal class ToasterWindows(
  IProcessManager processManager,
  ILogger<ToasterWindows> logger) : IToaster
{
  public Task ShowToast(string title, string message, ToastIcon toastIcon)
  {
    try
    {
      var script = GetToastScript(title, message, toastIcon);
      var bytes = Encoding.Unicode.GetBytes(script);
      var base64 = Convert.ToBase64String(bytes);

      var psi = new ProcessStartInfo
      {
        FileName = "powershell.exe",
        Arguments = $"-EncodedCommand {base64}",
        UseShellExecute = true,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
      };
      processManager.Start(psi);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while showing toast.");
    }

    return Task.CompletedTask;
  }

  private static string GetToastScript(string title, string message, ToastIcon balloonTipIcon)
  {
    var systemIcon = balloonTipIcon switch
    {
      ToastIcon.Info => "Information",
      ToastIcon.Warning => "Warning",
      ToastIcon.Error => "Error",
      _ => "None"
    };

    return $@"
$Host.UI.RawUI.WindowTitle = ""ControlR""
[System.Reflection.Assembly]::LoadWithPartialName(""System.Windows.Forms"")
[System.Reflection.Assembly]::LoadWithPartialName(""System.Drawing"")
$Notify = New-Object System.Windows.Forms.NotifyIcon
$Notify.Icon = [System.Drawing.SystemIcons]::{systemIcon}
$Notify.BalloonTipIcon = ""{balloonTipIcon}""
$Notify.BalloonTipTitle = ""{title}""
$Notify.BalloonTipText = ""{message}""
$Notify.Visible = $true
$Notify.ShowBalloonTip(5000)
$Notify.Dispose()";
  }
}