using System.Diagnostics;

namespace ControlR.Web.Server.Diagnostics;

internal static class DefaultActivitySource
{
  public const string SourceName = "ControlR.Web.Server";
  public static readonly ActivitySource Instance = new(SourceName);
  public static Activity? StartActivity(string name) => Instance.StartActivity(name);

  public static Activity? StartDeviceAccessActivity(
    string userName,
    Guid userId,
    Guid deviceId,
    IEnumerable<KeyValuePair<string, object?>>? tags = null)
  {
    var activity = Instance.StartActivity(
      DeviceAccessActivityNames.DeviceAccessSession,
      ActivityKind.Internal);

    if (activity is null)
    {
      return null;
    }

    activity.SetTag(ActivityTagKeys.UserId, userId);
    activity.SetTag(ActivityTagKeys.DeviceId, deviceId);
    activity.SetTag(ActivityTagKeys.UserName, userName);
    activity.SetTag(ActivityTagKeys.ActivityType, DeviceAccessActivityNames.DeviceAccessSession);
    
    if (tags is not null)
    {
      foreach (var tag in tags)
      {
        activity.AddTag(tag.Key, tag.Value);
      }
    }

    return activity;
  }
}
