using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Assist;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

/// <summary>
/// Control plane for ad-hoc remote control of unmanaged devices (issue 407). The assist app side
/// joins anonymously and is authorized by possession of the session id it created. The technician
/// side must additionally be a signed-in user holding the Flash Assist connect permission. Media
/// and input DTOs never pass through this hub. They ride the WebSocket relay, whose middleware
/// requires an authenticated requester.
/// </summary>
[AllowAnonymous]
public class AssistHub(
  UserManager<AppUser> userManager,
  IAssistSessionCache sessionCache,
  IPermissionEvaluator permissionEvaluator,
  IResourceDescriptorFactory resourceFactory,
  ILogger<AssistHub> logger)
  : HubWithItems<IAssistHubClient>, IAssistHub
{
  private const int MaxInvalidJoinAttempts = 10;

  private readonly ILogger<AssistHub> _logger = logger;
  private readonly IPermissionEvaluator _permissionEvaluator = permissionEvaluator;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;
  private readonly IAssistSessionCache _sessionCache = sessionCache;
  private readonly UserManager<AppUser> _userManager = userManager;

  public async Task<HubResult> CancelAssistConsent(CancelAssistConsentRequestDto request)
  {
    try
    {
      var session = await _sessionCache.TryGetAsync(request.SessionId, Context.ConnectionAborted);
      if (session is null)
      {
        return HubResult.Ok();
      }

      if (Context.ConnectionId != session.ViewerConnectionId)
      {
        return HubResult.Fail("Unauthorized.");
      }
 
      var returned = await _sessionCache.TryReturnToWaitingAsync(
        request.SessionId,
        request.ConsentNonce,
        Context.ConnectionAborted);
      if (returned is null)
      {
        return HubResult.Ok();
      }

      _logger.LogInformation(
        "Viewer connection {ViewerConnectionId} cancelled the consent request for assist session {SessionId}.",
        Context.ConnectionId,
        request.SessionId);

      await Clients
        .Client(session.AssistConnectionId)
        .AssistConsentCancelled(new(request.SessionId));

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while cancelling consent for assist session {SessionId}.", request.SessionId);
      return HubResult.Fail("An error occurred while cancelling the consent request.");
    }
  }

  public async Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession()
  {
    try
    {
      var session = await _sessionCache.CreateAsync(Context.ConnectionId, Context.ConnectionAborted);

      _logger.LogInformation(
        "Assist session {SessionId} created for connection {ConnectionId}.",
        session.SessionId,
        Context.ConnectionId);

      return HubResult.Ok(new CreateAssistSessionResponseDto(session.Code, session.SessionId));
    }
    catch (AssistSessionCapacityExceededException ex)
    {
      _logger.LogWarning("Assist session creation rejected: {Reason}", ex.Message);
      return HubResult.Fail<CreateAssistSessionResponseDto>(
        "The server is busy with too many assist sessions right now. Try again later.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating an assist session.");
      return HubResult.Fail<CreateAssistSessionResponseDto>("An error occurred while creating the session.");
    }
  }

  public async Task<HubResult> EndAssistSession(EndAssistSessionRequestDto request)
  {
    try
    {
      var session = await _sessionCache.TryGetAsync(request.SessionId, Context.ConnectionAborted);
      if (session is null)
      {
        return HubResult.Ok();
      }

      if (!IsSessionParticipant(session))
      {
        return HubResult.Fail("Unauthorized.");
      }

      _ = await _sessionCache.RemoveAsync(request.SessionId, Context.ConnectionAborted);
      await NotifyOtherSide(session, "The session was ended by the remote user.");

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while ending assist session {SessionId}.", request.SessionId);
      return HubResult.Fail("An error occurred while ending the session.");
    }
  }

  public async Task<HubResult<JoinAssistSessionResponseDto>> JoinAssistSession(JoinAssistSessionRequestDto request)
  {
    try
    {
      if (Context.User?.TryGetUserId(out var userId) != true)
      {
        return HubResult.Fail<JoinAssistSessionResponseDto>("Unauthorized.");
      }

      if (!await CanTechnicianConnectAsync())
      {
        return HubResult.Fail<JoinAssistSessionResponseDto>(
          "You don't have permission to connect with Flash Assist. Ask an administrator for the Flash Assist permission.");
      }

      var attempts = GetItem(0, nameof(JoinAssistSession));
      if (attempts >= MaxInvalidJoinAttempts)
      {
        _logger.LogWarning(
          "User {UserId} exceeded the invalid assist code limit on connection {ConnectionId}.",
          userId,
          Context.ConnectionId);
        return HubResult.Fail<JoinAssistSessionResponseDto>("Too many invalid codes were entered. Reconnect and try again.");
      }

      var code = new string(request.Code.Where(char.IsDigit).ToArray());
      var session = code.Length == AssistSessionCache.CodeLength
        ? await _sessionCache.TryGetByCodeAsync(code, Context.ConnectionAborted)
        : null;

      if (session is null)
      {
        SetItem(attempts + 1, nameof(JoinAssistSession));
        return HubResult.Fail<JoinAssistSessionResponseDto>("The code is invalid or no longer active.");
      }

      var user = await _userManager.Users
        .AsNoTracking()
        .Include(x => x.UserPreferences)
        .FirstOrDefaultAsync(x => x.Id == userId, Context.ConnectionAborted);

      if (user is null)
      {
        _logger.LogError("User {UserId} not found while joining assist session.", userId);
        return HubResult.Fail<JoinAssistSessionResponseDto>("User not found.");
      }

      var displayName = GetDisplayName(user);

      var pending = await _sessionCache.TryBeginConsentAsync(
        session.SessionId,
        Context.ConnectionId,
        userId,
        displayName,
        user.Email,
        Context.ConnectionAborted);

      if (pending is null)
      {
        return HubResult.Fail<JoinAssistSessionResponseDto>("This device isn't accepting a new connection right now.");
      }

      _logger.LogInformation(
        "User {DisplayName} ({UserId}) requested assist session {SessionId} from IP {RemoteIp}.",
        displayName,
        userId,
        session.SessionId,
        Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString());

      await Clients
        .Client(pending.AssistConnectionId)
        .AssistConsentRequested(new(
          pending.SessionId,
          displayName,
          user.Email,
          pending.ConsentNonce ?? Guid.Empty));

      return HubResult.Ok(new JoinAssistSessionResponseDto(
        pending.SessionId,
        pending.ConsentNonce ?? Guid.Empty));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while joining assist session.");
      return HubResult.Fail<JoinAssistSessionResponseDto>("An error occurred while joining the session.");
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    // ConnectionAborted is already canceled once disconnect handlers run, so cleanup gets its own
    // bounded token instead.
    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var cleanupToken = cleanupCts.Token;

    try
    {
      var assistSessions = await _sessionCache.RemoveByAssistConnectionIdAsync(
        Context.ConnectionId,
        cleanupToken);

      foreach (var session in assistSessions)
      {
        if (session.ViewerConnectionId is { Length: > 0 })
        {
          await NotifyViewer(session, "The remote user disconnected.");
        }
      }

      // A technician dropping the connection during consent is treated like a cancel. The session
      // returns to waiting so the customer keeps the same code. An active session is torn down.
      var viewerSessions = (await _sessionCache.SnapshotAsync(cleanupToken))
        .Where(x => x.ViewerConnectionId == Context.ConnectionId)
        .ToList();

      foreach (var session in viewerSessions)
      {
        var returned = session.Status == AssistSessionStatus.PendingConsent
          ? await _sessionCache.TryReturnToWaitingAsync(
            session.SessionId,
            session.ConsentNonce ?? Guid.Empty,
            cleanupToken)
          : null;

        if (returned is not null)
        {
          await Clients
            .Client(session.AssistConnectionId)
            .AssistConsentCancelled(new(session.SessionId));
          continue;
        }

        if (await _sessionCache.RemoveAsync(session.SessionId, cleanupToken) is not null)
        {
          await NotifyAssist(session, "The technician disconnected.");
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while cleaning up assist sessions for connection {ConnectionId}.", Context.ConnectionId);
    }

    await base.OnDisconnectedAsync(exception);
  }

  public async Task<HubResult> RespondToAssistConsent(RespondToAssistConsentRequestDto request)
  {
    try
    {
      var session = await _sessionCache.TryGetAsync(request.SessionId, Context.ConnectionAborted);
      if (session is null)
      {
        return HubResult.Fail("The session was not found.");
      }

      if (Context.ConnectionId != session.AssistConnectionId)
      {
        return HubResult.Fail("Unauthorized.");
      }

      if (!request.Approved)
      {
        var returned = await _sessionCache.TryReturnToWaitingAsync(
          request.SessionId,
          request.ConsentNonce,
          Context.ConnectionAborted);
        if (returned is not null && session.ViewerConnectionId is { Length: > 0 })
        {
          await NotifyViewer(
            session,
            approved: false,
            accessToken: null,
            AssistConsentRespondedReason.Declined);
        }
        return HubResult.Ok();
      }

      var activated = await _sessionCache.TryActivateAsync(
        request.SessionId,
        request.ConsentNonce,
        Context.ConnectionAborted);
      if (activated?.ViewerConnectionId is not { Length: > 0 } viewerConnectionId)
      {
        return HubResult.Fail("The consent request is no longer pending.");
      }

      _logger.LogInformation(
        "Assist session {SessionId} accepted by the remote user for viewer connection {ViewerConnectionId}.",
        request.SessionId,
        viewerConnectionId);

      await Clients
        .Client(viewerConnectionId)
        .AssistConsentResponded(new(request.SessionId, true, activated.AccessToken));

      await Clients
        .Client(activated.AssistConnectionId)
        .AssistSessionStarted(new(
          request.SessionId,
          activated.AccessToken,
          activated.ViewerDisplayName ?? "technician",
          activated.ViewerEmail));

      return HubResult.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while responding to consent for assist session {SessionId}.", request.SessionId);
      return HubResult.Fail("An error occurred while responding to the consent request.");
    }
  }

  private static string GetDisplayName(AppUser user)
  {
    var displayName = user.UserPreferences
      ?.FirstOrDefault(x => x.Name == UserPreferenceNames.UserDisplayName)
      ?.Value;

    return string.IsNullOrWhiteSpace(displayName)
      ? user.Email ?? user.UserName ?? "a technician"
      : displayName;
  }

  /// <summary>
  /// Tenant-scoped capability check for the technician side of an assist session. A server
  /// principal has no tenant claim, so it is evaluated against the server resource; the
  /// permission itself only grants at tenant scope.
  /// </summary>
  private async Task<bool> CanTechnicianConnectAsync()
  {
    if (Context.User?.ToPrincipalDescriptor() is not { } principal)
    {
      return false;
    }

    var resource = principal.TenantId is { } tenantId
      ? _resourceFactory.CreateTenant(tenantId)
      : _resourceFactory.CreateServer();

    var decision = await _permissionEvaluator.Evaluate(
      principal,
      PermissionNames.AssistSessionsConnect,
      resource,
      Context.ConnectionAborted);

    if (!decision.Allowed)
    {
      _logger.LogInformation(
        "User {UserId} denied {Permission} while joining assist session.",
        principal.PrincipalId,
        PermissionNames.AssistSessionsConnect);
    }

    return decision.Allowed;
  }

  private bool IsSessionParticipant(AssistSession session)
  {
    if (Context.ConnectionId == session.AssistConnectionId)
    {
      return true;
    }

    return session.ViewerUserId is { Length: > 0 } viewerUserId &&
      Context.User?.TryGetUserId(out var userId) == true &&
      userId.ToString() == viewerUserId;
  }

  private async Task NotifyAssist(AssistSession session, string reason)
  {
    await Clients
      .Client(session.AssistConnectionId)
      .AssistSessionEnded(new(session.SessionId, reason));
  }

  private async Task NotifyOtherSide(AssistSession session, string reason)
  {
    if (Context.ConnectionId == session.AssistConnectionId)
    {
      if (session.ViewerConnectionId is { Length: > 0 })
      {
        await NotifyViewer(session, reason);
      }
      return;
    }

    await NotifyAssist(session, reason);
  }

  private async Task NotifyViewer(AssistSession session, string reason)
  {
    if (session.ViewerConnectionId is not { Length: > 0 } viewerConnectionId)
    {
      return;
    }

    await Clients
      .Client(viewerConnectionId)
      .AssistSessionEnded(new(session.SessionId, reason));
  }

  private async Task NotifyViewer(AssistSession session, bool approved, string? accessToken)
  {
    await NotifyViewer(session, approved, accessToken, reason: null);
  }

  private async Task NotifyViewer(
    AssistSession session,
    bool approved,
    string? accessToken,
    AssistConsentRespondedReason? reason)
  {
    if (session.ViewerConnectionId is not { Length: > 0 } viewerConnectionId)
    {
      return;
    }

    await Clients
      .Client(viewerConnectionId)
      .AssistConsentResponded(new(session.SessionId, approved, accessToken, reason));
  }
}
