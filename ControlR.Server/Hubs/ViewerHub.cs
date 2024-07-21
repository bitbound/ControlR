using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.Http;
using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Options;
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
    IHubContext<StreamerHub, IStreamerHubClient> _streamerHub,
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

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, SignedPayloadDto requestDto)
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

    public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto)
    {
        try
        {
            return await _agentHub.Clients.Client(agentConnectionId).GetAgentAppSettings(signedDto);
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

            var agentResult = await _connectionCounter.GetAgentConnectionCount();
            var viewerResult = await _connectionCounter.GetViewerConnectionCount();
            var streamerResult = await _connectionCounter.GetStreamerConnectionCount();

            if (!agentResult.IsSuccess)
            {
                _logger.LogResult(agentResult);
                return Result.Fail<ServerStatsDto>(agentResult.Reason);
            }

            if (!viewerResult.IsSuccess)
            {
                _logger.LogResult(viewerResult);
                return Result.Fail<ServerStatsDto>(viewerResult.Reason);
            }

            if (!streamerResult.IsSuccess)
            {
                _logger.LogResult(streamerResult);
                return Result.Fail<ServerStatsDto>(streamerResult.Reason);
            }

            var dto = new ServerStatsDto(
                agentResult.Value,
                viewerResult.Value,
                streamerResult.Value);

            return Result.Ok(dto);
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

    public async Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto)
    {
        try
        {
            return await _agentHub.Clients.Client(agentConnectionId).GetWindowsSessions(signedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting Windows sessions from agent.");
            return [];
        }
    }

    public override async Task OnConnectedAsync()
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

            var getResult = await GetServerStats();
            if (getResult.IsSuccess)
            {
                await Clients.Caller.ReceiveServerStats(getResult.Value);
            }
            else
            {
                getResult.Log(_logger);
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionCounter.DecrementViewerCount();
        await SendUpdatedConnectionCountToAdmins();

        if (Context.User?.TryGetClaim(ClaimNames.PublicKey, out var publicKey) == true)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, publicKey);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<Result> RequestStreamingSession(
        string agentConnectionId,
        SignedPayloadDto sessionRequestDto)
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

    public async Task<Result> SendAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto)
    {
        try
        {
            return await _agentHub.Clients.Client(agentConnectionId).ReceiveAgentAppSettings(signedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending agent appsettings.");
            return Result.Fail("Failed to send agent app settings.");
        }
    }

    public async Task<Result> SendAlertBroadcast(SignedPayloadDto signedDto)
    {
        try
        {
            using var scope = _logger.BeginMemberScope();

            var alert = MessagePackSerializer.Deserialize<AlertBroadcastDto>(signedDto.Payload);
            if (alert is null)
            {
                return Result
                    .Fail("Failed to deserialize alert payload.")
                    .Log(_logger);
            }

            var storeResult = await _alertStore.StoreAlert(alert);
            if (!storeResult.IsSuccess)
            {
                return storeResult;
            }

            await Clients.All.ReceiveAlertBroadcast(alert);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result
                .Fail(ex, "Failed to send agent app settings.")
                .Log(_logger);
        }
    }

    public async Task SendSignedDtoToAgent(string deviceId, SignedPayloadDto signedDto)
    {
        using var scope = _logger.BeginMemberScope();

        await _agentHub.Clients.Group(deviceId).ReceiveDto(signedDto);
    }

    public async Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto)
    {
        using var _ = _logger.BeginMemberScope();

        if (Context.User is null ||
            !Context.User.TryGetPublicKey(out var publicKey))
        {
            _logger.LogCritical("Failed to get public key from principal.");
            return;
        }

        await _agentHub.Clients.Group(publicKey).ReceiveDto(signedDto);
    }

    public async Task SendSignedDtoToStreamer(string streamerConnectionId, SignedPayloadDto signedDto)
    {
        try
        {
            using var scope = _logger.BeginMemberScope();

            await _streamerHub.Clients
                .Client(streamerConnectionId)
                .ReceiveDto(signedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending DTO to streamer.");
        }
    }

    public async Task<Result> SendTerminalInput(string agentConnectionId, SignedPayloadDto dto)
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
            var getResult = await GetServerStats();

            if (getResult.IsSuccess)
            {
                await Clients
                    .Group(HubGroupNames.ServerAdministrators)
                    .ReceiveServerStats(getResult.Value);
            }
            else
            {
                _logger.LogResult(getResult);
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