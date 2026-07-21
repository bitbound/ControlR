using System.Diagnostics;
using ControlR.Libraries.Api.Contracts.Diagnostics;
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

    if (viewerHub.SessionActivity is {} sessionActivity)
    {
      using var childActivity = sessionActivity.StartChildActivity($"{RemoteAccessActivityNames.HubMethodInvoked}.{invocationContext.HubMethodName}");
      return await next(invocationContext);
    }

    return await next(invocationContext);
  }
}