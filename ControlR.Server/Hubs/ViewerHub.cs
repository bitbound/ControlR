using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Options;
using ControlR.Server.Services;
using ControlR.Server.Services.Interfaces;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace ControlR.Server.Hubs;

[Authorize]
public class ViewerHub(
    IHubContext<AgentHub, IAgentHubClient> _agentHub,
    IServerStatsProvider _serverStatsProvider,
    IConnectionCounter _connectionCounter,
    IAlertStore _alertStore,
    IIpApi _ipApi,
    IWsBridgeApi _wsBridgeApi,
    IOptionsMonitor<ApplicationOptions> _appOptions,
    ILogger<ViewerHub> _logger) : HubWithItems<IViewerHubClient>, IViewerHub
{
    public Task<bool> CheckIfServerAdministrator()
    {
        return IsServerAdmin().AsTaskResult();
    }

    public async Task<Result> ClearAlert()
    {
        using var scope = _logger.BeginMemberScope();

        if (!VerifyIsAdmin())
        {
            return Result.Fail("Unauthorized.");
        }

        return await _alertStore.ClearAlert();
    }

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, TerminalSessionRequest requestDto)
    {
        try
        {
            return await _agentHub.Clients
                .Client(agentConnectionId)
                .CreateTerminalSession(requestDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating terminal session.");
            return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
        }
    }

    public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId)
    {
        try
        {
            return await _agentHub.Clients.Client(agentConnectionId).GetAgentAppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting agent appsettings.");
            return Result.Fail<AgentAppSettings>("Failed to get agent app settings.");
        }
    }

    public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
    {
        try
        {
            return await _alertStore.GetCurrentAlert();
        }
        catch (Exception ex)
        {
            return Result
                .Fail<AlertBroadcastDto>(ex, "Failed to get current alert.")
                .Log(_logger);
        }
    }

    public async Task<Result<ServerStatsDto>> GetServerStats()
    {
        try
        {
            if (!VerifyIsAdmin())
            {
                return Result.Fail<ServerStatsDto>("Unauthorized.");
            }

            return await _serverStatsProvider.GetServerStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting agent count.");
            return Result.Fail<ServerStatsDto>("Failed to get agent count.");
        }
    }

    public async Task<Uri?> GetWebSocketBridgeOrigin()
    {
        try
        {
            if (!_appOptions.CurrentValue.UseExternalWebSocketBridge ||
                 _appOptions.CurrentValue.ExternalWebSocketHosts.Count == 0)
            {
                return null;
            }

            var ipAddress = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            var result = await _ipApi.GetIpInfo(ipAddress);
            if (!result.IsSuccess)
            {
                return null;
            }


            var ipInfo = result.Value;

            if (ipInfo.Status == IpApiResponseStatus.Fail)
            {
                _logger.LogError("IpApi returned a failed status message.  Message: {IpMessage}", ipInfo.Message);
                return null;
            }

            var location = new Coordinate(ipInfo.Lat, ipInfo.Lon);
            var closest = CoordinateHelper.FindClosestCoordinate(location, _appOptions.CurrentValue.ExternalWebSocketHosts);
            if (closest.Origin is null || !await _wsBridgeApi.IsHealthy(closest.Origin))
            {
                return null;
            }
            return closest.Origin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting WebSocket bridge URI.");
            return null;
        }
    }

    public async Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId)
    {
        try
        {
            return await _agentHub.Clients.Client(agentConnectionId).GetWindowsSessions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting Windows sessions from agent.");
            return [];
        }
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            _connectionCounter.IncrementViewerCount();
            await SendUpdatedConnectionCountToAdmins();

            await base.OnConnectedAsync();

            if (Context.User is null)
            {
                _logger.LogWarning("User is null.  Authorize tag should have prevented this.");
                return;
            }

            if (!Context.User.TryGetPublicKey(out var publicKey))
            {
                _logger.LogWarning("Failed to get public key from viewer user.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, publicKey);

            if (IsServerAdmin())
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);

                var getResult = await _serverStatsProvider.GetServerStats();
                if (getResult.IsSuccess)
                {
                    await Clients.Caller.ReceiveServerStats(getResult.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during viewer connect.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _connectionCounter.DecrementViewerCount();
            await SendUpdatedConnectionCountToAdmins();
            // TODO: Remove from groups.
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during viewer disconnect.");
        }
    }

    public async Task<Result> RequestStreamingSession(
        string agentConnectionId,
        StreamerSessionRequestDto sessionRequestDto)
    {
        try
        {
            var sessionSuccess = await _agentHub.Clients
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
            return await _agentHub.Clients.Client(agentConnectionId).ReceiveAgentAppSettings(appSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending agent appsettings.");
            return Result.Fail("Failed to send agent app settings.");
        }
    }

    public async Task<Result> SendAlertBroadcast(AlertBroadcastDto alertDto)
    {
        try
        {
            using var scope = _logger.BeginMemberScope();

            var storeResult = await _alertStore.StoreAlert(alertDto);
            if (!storeResult.IsSuccess)
            {
                return storeResult;
            }

            await Clients.All.ReceiveAlertBroadcast(alertDto);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result
                .Fail(ex, "Failed to send agent app settings.")
                .Log(_logger);
        }
    }

    public async Task SendDtoToAgent(string deviceId, DtoWrapper wrapper)
    {
        using var scope = _logger.BeginMemberScope();

        await _agentHub.Clients.Group(deviceId).ReceiveDto(wrapper);
    }

    public Task SendDtoToUserGroups(DtoWrapper wrapper)
    {
        // TODO: Implement this.
        throw new NotImplementedException();
    }

    public async Task<Result> SendTerminalInput(string agentConnectionId, TerminalInputDto dto)
    {
        try
        {
            return await _agentHub.Clients.Client(agentConnectionId).ReceiveTerminalInput(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending terminal input.");
            return Result.Fail("Agent could not be reached.");
        }
    }

    private bool IsServerAdmin()
    {
        return Context.User?.IsAdministrator() ?? false;
    }

    private async Task SendUpdatedConnectionCountToAdmins()
    {
        try
        {
            var getResult = await _serverStatsProvider.GetServerStats();

            if (getResult.IsSuccess)
            {
                await Clients
                    .Group(HubGroupNames.ServerAdministrators)
                    .ReceiveServerStats(getResult.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
        }
    }

    private bool VerifyIsAdmin([CallerMemberName] string callerMember = "")
    {
        if (IsServerAdmin())
        {
            return true;
        }

        var publicKey = Context.User?.TryGetPublicKey(out var userPubKey) == true
             ? userPubKey
             : "Unknown";

        _logger.LogCritical(
            "Admin verification failed when invoking member {MemberName}. Public Key: {PublicKey}",
            callerMember,
            publicKey);

        return false;
    }
}