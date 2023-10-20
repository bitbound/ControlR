using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Helpers;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ControlR.Server.Hubs;

[Authorize]
public class ViewerHub(
    IHubContext<AgentHub, IAgentHubClient> agentHubContext,
    IHubContext<StreamerHub, IStreamerHubClient> streamerHubContext,
    IStreamerSessionCache streamerSessionCache,
    IOptionsMonitor<AppOptions> appOptions,
    ILogger<ViewerHub> logger) : Hub<IViewerHubClient>
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHubContext;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly ILogger<ViewerHub> _logger = logger;
    private readonly IHubContext<StreamerHub, IStreamerHubClient> _streamerHub = streamerHubContext;
    private readonly IStreamerSessionCache _streamerSessionCache = streamerSessionCache;

    public Task<IceServer[]> GetIceServers(SignedPayloadDto dto)
    {
        if (!VerifyPayload(dto, out _))
        {
            return Array.Empty<IceServer>().AsTaskResult();
        }

        return _appOptions.CurrentValue.IceServers.ToArray().AsTaskResult();
    }

    public async Task<Result<StreamerHubSession>> GetStreamingSession(string agentConnectionId, Guid streamingSessionId, SignedPayloadDto sessionRequestDto)
    {
        try
        {
            if (!VerifyPayload(sessionRequestDto, out _))
            {
                return Result.Fail<StreamerHubSession>(string.Empty);
            }

            var sessionSuccess = await _agentHub.Clients
                   .Client(agentConnectionId)
                   .GetStreamingSession(sessionRequestDto);

            if (!sessionSuccess)
            {
                return Result.Fail<StreamerHubSession>("Failed to acquire streaming session.");
            }

            await WaitHelper.WaitForAsync(
                () => _streamerSessionCache.Sessions.ContainsKey(streamingSessionId),
                TimeSpan.FromSeconds(30));

            if (!_streamerSessionCache.TryGetValue(streamingSessionId, out var session))
            {
                return Result.Fail<StreamerHubSession>("Timed out while waiting for streaming to start.");
            }

            session.AgentConnectionId = agentConnectionId;
            session.ViewerConnectionId = Context.ConnectionId;
            return Result.Ok(session);
        }
        catch (Exception ex)
        {
            return Result.Fail<StreamerHubSession>(ex);
        }
    }

    public async Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto)
    {
        if (!VerifyPayload(signedDto, out _))
        {
            return [];
        }

        return await _agentHub.Clients.Client(agentConnectionId).GetWindowsSessions(signedDto);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        if (Context.User?.TryGetClaim(ClaimNames.PublicKey, out var publicKey) != true)
        {
            _logger.LogWarning("Failed to get public key from viewer user.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, publicKey);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.TryGetClaim(ClaimNames.PublicKey, out var publicKey) == true)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, publicKey);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendSignedDtoToAgent(string deviceId, SignedPayloadDto signedDto)
    {
        using var scope = _logger.BeginMemberScope();

        if (!VerifyPayload(signedDto, out _))
        {
            return;
        }

        await _agentHub.Clients.Group(deviceId).ReceiveDto(signedDto);
    }

    public async Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto)
    {
        using var _ = _logger.BeginMemberScope();

        if (!VerifyPayload(signedDto, out var publicKey))
        {
            return;
        }

        await _agentHub.Clients.Group(publicKey).ReceiveDto(signedDto);
    }

    public async Task SendSignedDtoToStreamer(Guid streamingSessionId, SignedPayloadDto signedDto)
    {
        using var scope = _logger.BeginMemberScope();

        if (!VerifyPayload(signedDto, out _))
        {
            return;
        }

        if (!_streamerSessionCache.TryGetValue(streamingSessionId, out var session))
        {
            _logger.LogError("Session ID not found: {StreamerSessionId}", streamingSessionId);
            return;
        }

        await _streamerHub.Clients
            .Client(session.StreamerConnectionId)
            .ReceiveDto(signedDto);
    }

    private bool VerifyPayload(SignedPayloadDto signedDto, out string publicKey)
    {
        publicKey = string.Empty;

        if (Context.User?.TryGetClaim(ClaimNames.PublicKey, out publicKey) != true)
        {
            _logger.LogCritical("Failed to get public key from viewer user.");
            return false;
        }

        if (publicKey != signedDto.PublicKeyBase64)
        {
            _logger.LogCritical("Public key doesn't match what was retrieved during authentication.");
            return false;
        }

        return true;
    }
}