using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ControlR.Web.Client.Services;

public interface ISettings
{
  bool AppendInstanceIdToAgentInstall { get; set; }
  bool HideOfflineDevices { get; set; }
  bool NotifyUserSessionStart { get; set; }
  Task Reset();
}

internal class Settings(ILogger<Settings> logger) : ISettings
{
  private readonly ConcurrentDictionary<string, object?> _preferences = new();

  public bool AppendInstanceIdToAgentInstall
  {
    get => GetPref(false);
    set => SetPref(value);
  }

  public bool HideOfflineDevices
  {
    get => GetPref(true);
    set => SetPref(value);
  }

  public bool NotifyUserSessionStart
  {
    get => GetPref(false);
    set => SetPref(value);
  }

  public Task Reset()
  {
    _preferences.Clear();
    return Task.CompletedTask;
  }

  private T GetPref<T>(T defaultValue, [CallerMemberName] string callerMemberName = "")
  {
    try
    {
      if (_preferences.TryGetValue(callerMemberName, out var value) &&
          value is T typedValue)
      {
        return typedValue;
      }

      return defaultValue;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting preference for {MemberName}.", callerMemberName);
      return defaultValue;
    }
  }

  private void SetPref<T>(T newValue, [CallerMemberName] string callerMemmberName = "")
  {
    _preferences[callerMemmberName] = newValue;
  }
}