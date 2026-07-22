using System.Diagnostics;
using ControlR.Libraries.Shared.Diagnostics;

namespace ControlR.Web.Server.Diagnostics;

internal static class DefaultActivitySource
{
  public const string SourceName = "ControlR.Web.Server";
  public static readonly ActivitySource Instance = new(SourceName);
  public static Activity? StartActivity(string name) => Instance.StartActivity(name);

  public static Activity? StartRemoteAccessActivity(
    string userName,
    Guid userId,
    Guid deviceId,
    IEnumerable<KeyValuePair<string, object?>>? tags = null)
  {
    var activity = Instance.StartActivity(
      RemoteAccessActivityNames.RemoteAccessSession,
      ActivityKind.Internal);

    if (activity is null)
    {
      return null;
    }

    activity.SetTag(ActivityTagKeys.UserId, userId);
    activity.SetTag(ActivityTagKeys.DeviceId, deviceId);
    activity.SetTag(ActivityTagKeys.UserName, userName);
    
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
