using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
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

    public async Task<Result<TerminalSessionRequestResult>> CreateTerminalSession(string agentConnectionId, SignedPayloadDto requestDto)
    {
        try
        {
            if (!VerifySignature(requestDto, out _))
            {
                return Result.Fail<TerminalSessionRequestResult>("Signature verification failed.");
            }

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

    public async Task<VncSessionRequestResult> GetVncSession(string agentConnectionId, Guid sessionId, SignedPayloadDto sessionRequestDto)
    {
        try
        {
            if (!VerifySignature(sessionRequestDto, out _))
            {
                return new(false);
            }

            var signaler = new StreamSignaler(sessionId);
            _proxyStreamStore.AddOrUpdate(sessionId, signaler, (k, v) => signaler);

            var sessionResult = await _agentHub.Clients
                   .Client(agentConnectionId)
                   .GetVncSession(sessionRequestDto);

            return sessionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while requesting VNC session.");
            return new(false);
        }
    }

    public async Task<WindowsSession[]> GetWindowsSessions(string agentConnectionId, SignedPayloadDto signedDto)
    {
        if (!VerifySignature(signedDto, out _))
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

        if (!VerifySignature(signedDto, out _))
        {
            return;
        }

        await _agentHub.Clients.Group(deviceId).ReceiveDto(signedDto);
    }

    public async Task SendSignedDtoToPublicKeyGroup(SignedPayloadDto signedDto)
    {
        using var _ = _logger.BeginMemberScope();

        if (!VerifySignature(signedDto, out var publicKey))
        {
            return;
        }

        await _agentHub.Clients.Group(publicKey).ReceiveDto(signedDto);
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

    public async Task<Result> StartRdpProxy(string agentConnectionId, Guid sessionId, SignedPayloadDto requestDto)
    {
        try
        {
            if (!VerifySignature(requestDto, out _))
            {
                return new(false);
            }

            var signaler = new StreamSignaler(sessionId);
            _proxyStreamStore.AddOrUpdate(sessionId, signaler, (k, v) => signaler);

            var sessionResult = await _agentHub.Clients
                   .Client(agentConnectionId)
                   .StartRdpProxy(requestDto);

            return sessionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting RDP proxy session.");
            return new(false);
        }
    }

    private bool VerifySignature(SignedPayloadDto signedDto, out string publicKey)
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