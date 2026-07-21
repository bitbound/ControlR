using System.Diagnostics;

namespace ControlR.Web.Server.Diagnostics;

public static class ActivityExtensions
{
  public static Activity AddEvent(
    this Activity activity, 
    string eventName,
    ActivityTagsCollection? tags = null)
  {
    var activityEvent = new ActivityEvent(eventName, tags: tags);
    return activity.AddEvent(activityEvent);
  }

  public static Activity? StartChildActivity(this Activity parentActivity, string activityName)
  {
    var activity = parentActivity.Source.StartActivity(
      activityName,
      ActivityKind.Internal,
      parentActivity.Context);

    return activity;
  }
}