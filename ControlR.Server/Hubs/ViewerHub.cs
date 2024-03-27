using ControlR.Server.Auth;
using ControlR.Server.Extensions;
using ControlR.Server.Models;
using ControlR.Server.Options;
using ControlR.Server.Services;
using ControlR.Shared.Dtos;
using ControlR.Shared.Extensions;
using ControlR.Shared.Hubs;
using ControlR.Shared.Interfaces.HubClients;
using ControlR.Shared.Models;
using ControlR.Shared.Primitives;
using ControlR.Shared.Services;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ControlR.Server.Hubs;

[Authorize]
public class ViewerHub(
    IHubContext<AgentHub, IAgentHubClient> _agentHub,
    IHubContext<StreamerHub, IStreamerHubClient> _streamerHub,
    IConnectionCounter _connectionCounter,
    IAlertStore _alertStore,
    IStreamerSessionCache _streamerSessionCache,
    IOptionsMonitor<ApplicationOptions> _appOptions,
    IDelayer _delayer,
    ILogger<ViewerHub> _logger) : Hub<IViewerHubClient>, IViewerHub
{
    private bool IsServerAdmin
    {
        get
        {
            if (Context.Items.TryGetValue(nameof(IsServerAdmin), out var cachedItem) &&
                bool.TryParse($"{cachedItem}", out var isAdmin))
            {
                return isAdmin;
            }
            return false;
        }
        set
        {
            Context.Items[nameof(IsServerAdmin)] = value;
        }
    }

    public Task<IceServer[]> GetIceServers(SignedPayloadDto dto)
    {
        if (!VerifySignature(dto, out _))
        {
            return Array.Empty<IceServer>().AsTaskResult();
        }

        return _appOptions.CurrentValue.IceServers.ToArray().AsTaskResult();
    }

    public async Task<Result<StreamerHubSession>> GetStreamingSession(string agentConnectionId, Guid streamingSessionId, SignedPayloadDto sessionRequestDto)
    {
        try
        {
            if (!VerifySignature(sessionRequestDto, out _))
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

            // TODO: Change to AsyncManualResetEvent.
            await _delayer.WaitForAsync(
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

    public async Task SendSignedDtoToStreamer(Guid streamingSessionId, SignedPayloadDto signedDto)
    {
        using var scope = _logger.BeginMemberScope();

        if (!VerifySignature(signedDto, out _))
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

    public Task<bool> CheckIfServerAdministrator()
    {
        return IsServerAdmin.AsTaskResult();
    }

    public async Task<Result> ClearAlert(SignedPayloadDto signedDto)
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

    public Task<Result<ServerStatsDto>> GetServerStats()
    {
        try
        {
            if (!VerifyIsAdmin())
            {
                return Result.Fail<ServerStatsDto>("Unauthorized.").AsTaskResult();
            }

            var dto = new ServerStatsDto(
                _connectionCounter.AgentCount,
                _connectionCounter.ViewerCount);

            return Result.Ok(dto).AsTaskResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting agent count.");
            return Result.Fail<ServerStatsDto>("Failed to get agent count.").AsTaskResult();
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

        IsServerAdmin = Context.User?.IsAdministrator() ?? false;
        if (IsServerAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroupNames.ServerAdministrators);
            var statsDto = new ServerStatsDto(
                _connectionCounter.AgentCount,
                _connectionCounter.ViewerCount);
            await Clients.Caller.ReceiveServerStats(statsDto);
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

    public async Task<Result> SendAlertBroadcast(SignedPayloadDto signedDto)
    {
        try
        {
            using var scope = _logger.BeginMemberScope();

            if (!VerifySignature(signedDto, out _))
            {
                return Result.Fail("Signature verification failed.");
            }

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

    private async Task SendUpdatedConnectionCountToAdmins()
    {
        try
        {
            var dto = new ServerStatsDto(
                _connectionCounter.AgentCount,
                _connectionCounter.ViewerCount);

            await Clients
                .Group(HubGroupNames.ServerAdministrators)
                .ReceiveServerStats(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending updated agent connection count to admins.");
        }
    }

    private bool VerifyIsAdmin([CallerMemberName] string callerMember = "")
    {
        var publicKey = Context.User?.TryGetPublicKey(out var userPubKey) == true
            ? userPubKey
            : "Unknown";

        if (!IsServerAdmin)
        {
            _logger.LogCritical(
                "Admin verification failed when invoking membmer {MemberName}. Public Key: {PublicKey}",
                callerMember,
                publicKey);
        }
        return IsServerAdmin;
    }

    private bool VerifySignature(SignedPayloadDto signedDto, [NotNullWhen(true)] out string? publicKey)
    {
        publicKey = default;

        if (Context.User?.TryGetPublicKey(out publicKey) != true)
        {
            _logger.LogCritical("Failed to get public key from viewer user.");
            return false;
        }

        if (publicKey != signedDto.PublicKeyBase64)
        {
            _logger.LogCritical(
                "Public key doesn't match what was retrieved during authentication.  " +
                "Public Key: {ReceivedPublicKey}",
                publicKey);

            return false;
        }

        return true;
    }
}