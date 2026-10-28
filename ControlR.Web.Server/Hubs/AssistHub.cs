using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Services.Assist;
using Microsoft.AspNetCore.SignalR;

namespace ControlR.Web.Server.Hubs;

/// <summary>
/// Control plane for ad-hoc remote control of unmanaged devices (issue 407). The assist app side
/// joins anonymously and is authorized by possession of the session id it created. The technician
/// side must additionally be a signed-in user. Media and input DTOs never pass through this hub.
/// They ride the WebSocket relay, whose middleware requires an authenticated requester.
/// </summary>
[AllowAnonymous]
public class AssistHub(
  UserManager<AppUser> userManager,
  IAssistSessionCache sessionCache,
  ILogger<AssistHub> logger)
  : HubWithItems<IAssistHubClient>, IAssistHub
{
  private const int MaxInvalidJoinAttempts = 10;

  private readonly ILogger<AssistHub> _logger = logger;
  private readonly IAssistSessionCache _sessionCache = sessionCache;
  private readonly UserManager<AppUser> _userManager = userManager;

  public Task<HubResult<CreateAssistSessionResponseDto>> CreateAssistSession()
  {
    try
    {
      var session = _sessionCache.Create(Context.ConnectionId);

      _logger.LogInformation(
        "Assist session {SessionId} created for connection {ConnectionId}.",
        session.SessionId,
        Context.ConnectionId);

      return Task.FromResult(HubResult.Ok(new CreateAssistSessionResponseDto(session.Code, session.SessionId)));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while creating an assist session.");
      return Task.FromResult(HubResult.Fail<CreateAssistSessionResponseDto>("An error occurred while creating the session."));
    }
  }

  public async Task<HubResult> EndAssistSession(EndAssistSessionRequestDto request)
  {
    try
    {
      if (!_sessionCache.TryGet(request.SessionId, out var session) || session is null)
      {
        return HubResult.Ok();
      }

      if (!IsSessionParticipant(session))
      {
        return HubResult.Fail("Unauthorized.");
      }

      _ = _sessionCache.Remove(request.SessionId);
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
      if (code.Length != AssistSessionCache.CodeLength ||
          !_sessionCache.TryGetByCode(code, out var session) ||
          session is null)
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

      if (!_sessionCache.TryBeginConsent(
        session.SessionId,
        Context.ConnectionId,
        userId,
        displayName,
        user.Email,
        out var pending) ||
          pending is null)
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
        .AssistConsentRequested(new(pending.SessionId, displayName, user.Email));

      return HubResult.Ok(new JoinAssistSessionResponseDto(pending.SessionId));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while joining assist session.");
      return HubResult.Fail<JoinAssistSessionResponseDto>("An error occurred while joining the session.");
    }
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    try
    {
      foreach (var session in _sessionCache.RemoveByAssistConnectionId(Context.ConnectionId))
      {
        if (session.ViewerConnectionId is { Length: > 0 })
        {
          await NotifyViewer(session, "The remote user disconnected.");
        }
      }

      foreach (var session in _sessionCache.RemoveByViewerConnectionId(Context.ConnectionId))
      {
        await NotifyAssist(session, "The technician disconnected.");
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
      if (!_sessionCache.TryGet(request.SessionId, out var session) || session is null)
      {
        return HubResult.Fail("The session was not found.");
      }

      if (Context.ConnectionId != session.AssistConnectionId)
      {
        return HubResult.Fail("Unauthorized.");
      }

      if (!request.Approved)
      {
        if (_sessionCache.TryReturnToWaiting(request.SessionId, out _) &&
            session.ViewerConnectionId is { Length: > 0 })
        {
          await NotifyViewer(session, approved: false, accessToken: null);
        }
        return HubResult.Ok();
      }

      if (!_sessionCache.TryActivate(request.SessionId, out var activated) ||
          activated is null ||
          activated.ViewerConnectionId is not { Length: > 0 } viewerConnectionId)
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
    if (session.ViewerConnectionId is not { Length: > 0 } viewerConnectionId)
    {
      return;
    }

    await Clients
      .Client(viewerConnectionId)
      .AssistConsentResponded(new(session.SessionId, approved, accessToken));
  }
}
