using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ControlR.Server.Hubs;

[Authorize]
public class ViewerHub(
    IHubContext<AgentHub, IAgentHubClient> agentHubContext,
    IProxyStreamStore proxyStreamStore,
    IOptionsMonitor<AppOptions> appOptions,
    ILogger<ViewerHub> logger) : Hub<IViewerHubClient>
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub = agentHubContext;
    private readonly IOptionsMonitor<AppOptions> _appOptions = appOptions;
    private readonly ILogger<ViewerHub> _logger = logger;
    private readonly IProxyStreamStore _proxyStreamStore = proxyStreamStore;

    public Task<IceServer[]> GetIceServers(SignedPayloadDto dto)
    {
        if (!VerifyPayload(dto, out _))
        {
            return Array.Empty<IceServer>().AsTaskResult();
        }

        return _appOptions.CurrentValue.IceServers.ToArray().AsTaskResult();
    }

    public async Task<Result> GetVncSession(string agentConnectionId, Guid sessionId, SignedPayloadDto sessionRequestDto)
    {
        try
        {
            if (!VerifyPayload(sessionRequestDto, out _))
            {
                return Result.Fail(string.Empty);
            }

            var signaler = new StreamSignaler(sessionId);
            _proxyStreamStore.AddOrUpdate(sessionId, signaler, (k, v) => signaler);

            var sessionSuccess = await _agentHub.Clients
                   .Client(agentConnectionId)
                   .GetVncSession(sessionRequestDto);

            if (!sessionSuccess)
            {
                return Result.Fail("Failed to acquire VNC session.");
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
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