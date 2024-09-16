namespace ControlR.Web.Client.Services;

public interface IScreenWake
{
  Task SetScreenWakeLock(bool isWakeEnabled);
}
public class ScreenWake : IScreenWake
{
  public Task SetScreenWakeLock(bool isWakeEnabled)
  {
    // TODO.
    return Task.CompletedTask;
  }
}
