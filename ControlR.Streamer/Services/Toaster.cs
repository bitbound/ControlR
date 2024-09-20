using System.Diagnostics;
using System.Text;

namespace ControlR.Streamer.Services;

public interface IToaster
{
  Task ShowToast(string title, string message, BalloonTipIcon balloonTipIcon);
}

internal class Toaster(
  IProcessManager processManager,
  ILogger<Toaster> logger) : IToaster
{
  public Task ShowToast(string title, string message, BalloonTipIcon balloonTipIcon)
  {
    try
    {
      var script = GetToastScript(title, message, balloonTipIcon);
      var bytes = Encoding.Unicode.GetBytes(script);
      var base64 = Convert.ToBase64String(bytes);

      var psi = new ProcessStartInfo
      {
        FileName = "powershell.exe",
        Arguments = $"-EncodedCommand {base64}",
        UseShellExecute = true,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
      };
      processManager.Start(psi);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while showing toast.");
    }

    return Task.CompletedTask;
  }

  private static string GetToastScript(string title, string message, BalloonTipIcon balloonTipIcon)
  {
    var systemIcon = balloonTipIcon switch
    {
      BalloonTipIcon.Info => "Information",
      BalloonTipIcon.Warning => "Warning",
      BalloonTipIcon.Error => "Error",
      _ => "None"
    };

    return $@"
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

public enum BalloonTipIcon
{
  None,
  Info,
  Warning,
  Error
}