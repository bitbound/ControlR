using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

public partial class ViewerHubTraceFilter : IHubFilter
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
  private static readonly Lazy<Dictionary<string, string>> _snakeCaseMap = new(() =>
    _includedMethods.ToDictionary(
      name => name,
      name => ToLowerSnakeCase(name.AsSpan()),
      StringComparer.Ordinal));

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
      using var childActivity = sessionActivity.StartChildActivity(GetSnakeCaseName(invocationContext.HubMethodName));
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

  private static string GetSnakeCaseName(string methodName) =>
    _snakeCaseMap.Value.TryGetValue(methodName, out var name) ? name : methodName;

  private static string ToLowerSnakeCase(ReadOnlySpan<char> name)
  {
    if (name.IsEmpty)
    {
      return string.Empty;
    }

    var sb = new StringBuilder(name.Length + 4);
    for (var i = 0; i < name.Length; i++)
    {
      var c = name[i];
      if (i > 0 && char.IsUpper(c))
      {
        var prev = name[i - 1];
        var isPrevLower = char.IsLower(prev);
        var isNextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
        if (isPrevLower || (char.IsUpper(prev) && isNextLower))
        {
          sb.Append('_');
        }
      }

      sb.Append(char.ToLowerInvariant(c));
    }
    return sb.ToString();
  }
}