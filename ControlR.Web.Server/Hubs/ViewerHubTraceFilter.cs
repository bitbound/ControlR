using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public class ViewerHubTraceFilter : IHubFilter
{
  private static readonly HashSet<string> _includedMethods = 
  [
    nameof(ViewerHub.CloseChatSession),
    nameof(ViewerHub.CloseTerminalSession),
    nameof(ViewerHub.CreateTerminalSession),
    nameof(ViewerHub.GetPwshCompletions),
    nameof(ViewerHub.InvokeCtrlAltDel),
    nameof(ViewerHub.RequestRemoteControlPermission),
    nameof(ViewerHub.RequestRemoteControlSession),
    nameof(ViewerHub.RequestVncSession),
    nameof(ViewerHub.SendAgentUpdateTrigger),
    nameof(ViewerHub.SendChatMessage),
    nameof(ViewerHub.SendPowerStateChange),
    nameof(ViewerHub.SendTerminalInput),
    nameof(ViewerHub.SendWakeDevice),
    nameof(ViewerHub.TestVncConnection),
    nameof(ViewerHub.UploadFile),
    nameof(ViewerHub.UninstallAgent),
  ];

  public async ValueTask<object?> InvokeMethodAsync(
    HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
  {
    if (invocationContext.Hub is not ViewerHub viewerHub)
    {
      return await next(invocationContext);
    }

    if (_includedMethods.Contains(invocationContext.HubMethodName) && 
        viewerHub.SessionActivity is {} sessionActivity)
    {
      using var childActivity = sessionActivity.StartChildActivity($"{invocationContext.HubMethodName}");
      try
      {

        var result = await next(invocationContext);

        var isSuccess = result switch
        {
          HubResult hubResult => hubResult.IsSuccess,
          _ => true
        };

        var statusCode = isSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error;
        childActivity?.SetStatus(statusCode);
        return result;
      }
      catch
      {
        childActivity?.SetStatus(ActivityStatusCode.Error);
        throw;
      }
    }

    return await next(invocationContext);
  }
}