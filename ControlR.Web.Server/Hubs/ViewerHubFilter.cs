using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public class ViewerHubFilter : IHubFilter
{
  public async ValueTask<object?> InvokeMethodAsync(
    HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
  {
    if (invocationContext.Hub is not ViewerHub viewerHub)
    {
      return await next(invocationContext);
    }

    if (Activity.Current is {} currentActivity && 
        currentActivity.ParentId is null &&
        viewerHub.SessionActivity?.Id is {} sessionActivityId)
    {
      Activity.Current.SetParentId(sessionActivityId);
    }

    try
    {
      return await next(invocationContext);
    }
    catch (Exception ex)
    {
      
      Console.WriteLine($"Exception calling '{invocationContext.HubMethodName}': {ex}");
      throw;
    }
  }
}