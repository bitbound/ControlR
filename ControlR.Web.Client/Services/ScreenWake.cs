namespace ControlR.Web.Client.Services;

public interface IScreenWake
{
  Task SetScreenWakeLock(bool isWakeEnabled);
}
public class ScreenWake(
  IJsInterop jsInterop,
  ILogger<ScreenWake> logger) : IScreenWake
{
  private readonly IJsInterop _jsInterop = jsInterop;
  private readonly ILogger<ScreenWake> _logger = logger;
  public async Task SetScreenWakeLock(bool isWakeEnabled)
  {
    try
    {
      await _jsInterop.SetScreenWakeLock(isWakeEnabled);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while requesting screen wake lock.");
    }
  }
}
