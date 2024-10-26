﻿using System.Runtime.CompilerServices;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Web.Client.Authz;
using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Data.Entities;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

[Authorize]
public class ViewerHub(
  AppDb _appDb,
  UserManager<AppUser> _userManager,
  IAuthorizationService _authzService,
  IHubContext<AgentHub, IAgentHubClient> agentHub,
  IServerStatsProvider serverStatsProvider,
  IConnectionCounter connectionCounter,
  IIpApi ipApi,
  IWsBridgeApi wsBridgeApi,
  IOptionsMonitor<AppOptions> appOptions,
  ILogger<ViewerHub> logger) : HubWithItems<IViewerHubClient>, IViewerHub
{
  public Task<bool> CheckIfServerAdministrator()
  {
    return IsServerAdmin().AsTaskResult();
  }

  public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(
    string agentConnectionId,
    TerminalSessionRequest requestDto)
  {
    try
    {
      return await agentHub.Clients
        .Client(agentConnectionId)
        .CreateTerminalSession(requestDto);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
    }
  }

  public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
  {
    try
    {
      return await agentHub.Clients.Client(agentConnectionId).GetAgentAppSettings();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting agent appsettings.");
      return Result.Fail<AgentAppSettings>("Failed to get agent app settings.");
    }
  }


  public async Task<Result<ServerStatsDto>> GetServerStats()
  {
    try
    {
      if (!VerifyIsServerAdmin())
      {
        return Result.Fail<ServerStatsDto>("Unauthorized.");
      }

      return await serverStatsProvider.GetServerStats();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting agent count.");
      return Result.Fail<ServerStatsDto>("Failed to get agent count.");
    }
  }

  public async Task<Uri?> GetWebSocketBridgeOrigin()
  {
    try
    {
      if (!appOptions.CurrentValue.UseExternalWebSocketBridge ||
          appOptions.CurrentValue.ExternalWebSocketHosts.Count == 0)
      {
        return null;
      }

      var ipAddress = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString();
      if (string.IsNullOrWhiteSpace(ipAddress))
      {
        return null;
      }

      var result = await ipApi.GetIpInfo(ipAddress);
      if (!result.IsSuccess)
      {
        return null;
      }


      var ipInfo = result.Value;

      if (ipInfo.Status == IpApiResponseStatus.Fail)
      {
        logger.LogError("IpApi returned a failed status message.  Message: {IpMessage}", ipInfo.Message);
        return null;
      }

      var location = new Coordinate(ipInfo.Lat, ipInfo.Lon);
      var closest = CoordinateHelper.FindClosestCoordinate(location, appOptions.CurrentValue.ExternalWebSocketHosts);
      if (closest.Origin is null || !await wsBridgeApi.IsHealthy(closest.Origin))
      {
        return null;
      }

      return closest.Origin;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting WebSocket bridge URI.");
      return null;
    }
  }

  public async Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId)
  {
    try
    {
      return await agentHub.Clients.Client(agentConnectionId).GetWindowsSessions();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while getting Windows sessions from agent.");
      return [];
    }
  }

  public override async Task OnConnectedAsync()
  {
    try
    {
      await base.OnConnectedAsync();

      connectionCounter.IncrementViewerCount();
      await SendUpdatedConnectionCountToAdmins();

      if (Context.User is null)
      {
        logger.LogCritical("User is null.  Authorize tag should have prevented this.");
        return;
      }

      if (!Context.User.TryGetTenantId(out var tenantId))
      {
        logger.LogCritical("Failed to get tenant ID.");
        return;
      }

      if (Context.User.IsInRole(RoleNames.ServerAdministrator))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);

        var getResult = await serverStatsProvider.GetServerStats();
        if (getResult.IsSuccess)
        {
          await Clients.Caller.ReceiveServerStats(getResult.Value);
        }
      }
      
      if (Context.User.IsInRole(RoleNames.TenantAdministrator))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetUserRoleGroupName(RoleNames.TenantAdministrator, tenantId));
      }

      if (Context.User.IsInRole(RoleNames.DeviceSuperUser))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetUserRoleGroupName(RoleNames.DeviceSuperUser, tenantId));
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error during viewer connect.");
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      await base.OnDisconnectedAsync(exception);

      connectionCounter.DecrementViewerCount();
      await SendUpdatedConnectionCountToAdmins();

      if (Context.User is null)
      {
        return;
      }

      if (Context.User.IsInRole(RoleNames.ServerAdministrator))
      {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);
      }

      if (!Context.User.TryGetTenantId(out var tenantId))
      {
        return;
      }

      if (Context.User.IsInRole(RoleNames.TenantAdministrator))
      {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroupNames.GetUserRoleGroupName(RoleNames.TenantAdministrator, tenantId));
      }

      if (Context.User.IsInRole(RoleNames.DeviceSuperUser))
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.GetUserRoleGroupName(RoleNames.DeviceSuperUser, tenantId));
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error during viewer disconnect.");
    }
  }

  public async Task<Result> RequestStreamingSession(
    string agentConnectionId,
    StreamerSessionRequestDto sessionRequestDto)
  {
    try
    {
      // TODO: Get user name from IUserStore
      //var name = Context.User?.Identity?.Name;
      //sessionRequestDto = sessionRequestDto with { ViewerName = name ?? "" };

      var sessionSuccess = await agentHub.Clients
        .Client(agentConnectionId)
        .CreateStreamingSession(sessionRequestDto);

      if (!sessionSuccess)
      {
        return Result.Fail("Failed to request a streaming session from the agent.");
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex);
    }
  }

  public async Task<Result> SendAgentAppSettings(string agentConnectionId, AgentAppSettings appSettings)
  {
    try
    {
      return await agentHub.Clients.Client(agentConnectionId).ReceiveAgentAppSettings(appSettings);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending agent appsettings.");
      return Result.Fail("Failed to send agent app settings.");
    }
  }

  public async Task SendDtoToAgent(Guid deviceId, DtoWrapper wrapper)
  {
    using var scope = logger.BeginMemberScope();

    await agentHub.Clients.Group(HubGroupNames.GetDeviceGroupName(deviceId)).ReceiveDto(wrapper);
  }

  public async Task SendDtoToUserGroups(DtoWrapper wrapper)
  {
    // TODO: Implement this.
    await Task.Yield();
  }

  public async Task<Result> SendTerminalInput(string agentConnectionId, TerminalInputDto dto)
  {
    try
    {
      return await agentHub.Clients.Client(agentConnectionId).ReceiveTerminalInput(dto);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending terminal input.");
      return Result.Fail("Agent could not be reached.");
    }
  }

  private bool IsServerAdmin()
  {
    return Context.User?.IsInRole(RoleNames.ServerAdministrator) ?? false;
  }

  private async Task SendUpdatedConnectionCountToAdmins()
  {
    try
    {
      var getResult = await serverStatsProvider.GetServerStats();

      if (getResult.IsSuccess)
      {
        await Clients
          .Group(HubGroupNames.ServerAdministrators)
          .ReceiveServerStats(getResult.Value);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while sending updated agent connection count to admins.");
    }
  }

  private bool VerifyIsServerAdmin([CallerMemberName] string callerMember = "")
  {
    if (IsServerAdmin())
    {
      return true;
    }

    var userName = Context.User?.Identity?.Name;
    logger.LogCritical(
      "Admin verification failed when invoking member {MemberName}. User: {UserName}",
      callerMember,
      userName);

    return false;
  }
}