using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Hubs;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Runtime.CompilerServices;

namespace ControlR.Server.Hubs;

[Authorize]
public class ViewerHub(
    IHubContext<AgentHub, IAgentHubClient> _agentHub,
    IProxyStreamStore _proxyStreamStore,
    IAgentConnectionCounter _agentCounter,
    ILogger<ViewerHub> _logger) : Hub<IViewerHubClient>, IViewerHub
{

    public async Task<Result<bool>> CheckIfServerAdministrator(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignature(signedDto, out _))
            {
                return Result.Fail<bool>("Signature verification failed.");
            }

            var isAdmin = Context.User?.IsAdministrator() ?? false;
            if (isAdmin)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);
                await Clients.Caller.ReceiveAgentConnectionCount(_agentCounter.AgentCount);
            }
            return Result.Ok(isAdmin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while check if user is an administrator.");
            return Result.Fail<bool>("An error occurred.");
        }
    }

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

    public async Task<Result<AgentAppSettings>> GetAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignature(signedDto, out _))
            {
                return Result.Fail<AgentAppSettings>("Signature verification failed.");
            }

            return await _agentHub.Clients.Client(agentConnectionId).GetAgentAppSettings(signedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting agent appsettings.");
            return Result.Fail<AgentAppSettings>("Failed to get agent app settings.");
        }
    }

    public Task<Result<int>> GetAgentCount(SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignature(signedDto, out _))
            {
                return Result.Fail<int>("Signature verification failed.").AsTaskResult();
            }

            var adminResult = VerifyIsAdmin(signedDto);
            if (!adminResult.IsSuccess || !adminResult.Value)
            {
                return Result.Fail<int>("Failed to verify administrator access.").AsTaskResult();
            }

            return Result.Ok(_agentCounter.AgentCount).AsTaskResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting agent count.");
            return Result.Fail<int>("Failed to get agent count.").AsTaskResult();
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
        try
        {
            if (!VerifySignature(signedDto, out _))
            {
                return [];
            }

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

    public async Task<Result> SendAgentAppSettings(string agentConnectionId, SignedPayloadDto signedDto)
    {
        try
        {
            if (!VerifySignature(signedDto, out _))
            {
                return Result.Fail("Signature verification failed.");
            }

            return await _agentHub.Clients.Client(agentConnectionId).ReceiveAgentAppSettings(signedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending agent appsettings.");
            return Result.Fail("Failed to sending agent app settings.");
        }
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

    private Result<bool> VerifyIsAdmin(
        SignedPayloadDto signedDto,
        [CallerMemberName] string callerMember = "")
    {
        if (!VerifySignature(signedDto, out var publicKey))
        {
            var result = Result.Fail<bool>("Signature verification failed.");
            _logger.LogResult(result);
            return result;
        }

        var isAdmin = Context.User?.IsAdministrator() ?? false;
        if (!isAdmin)
        {
            _logger.LogCritical(
                "Admin verification failed when invoking membmer {MemberName}. Public Key: {PublicKey}",
                callerMember,
                publicKey);
        }
        return Result.Ok(isAdmin);
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