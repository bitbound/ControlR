using ControlR.DesktopClient.Common.Options;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

/// <summary>
/// Responder-side authority on inbound viewer commands. The WebSocket relay is payload-opaque and
/// its allow is never a grant, so every command is checked against the server-sealed command
/// policy carried at session start before the real handler sees it. A relay drop is only an
/// optimization. This gate is authority.
/// </summary>
internal sealed class CommandPolicyViewerCommandHandler : IRemoteControlViewerCommandHandler
{
  private readonly HashSet<RemoteControlViewerDtoType>? _allowedDtoTypes;
  private readonly IRemoteControlViewerCommandHandler _innerHandler;
  private readonly ILogger<CommandPolicyViewerCommandHandler> _logger;
  private readonly RemoteControlCommandPolicyDto? _policy;

  public CommandPolicyViewerCommandHandler(
    IRemoteControlViewerCommandHandler innerHandler,
    IOptions<RemoteControlSessionOptions> sessionOptions,
    ILogger<CommandPolicyViewerCommandHandler> logger)
  {
    _innerHandler = innerHandler;
    _logger = logger;
    _policy = sessionOptions.Value.CommandPolicy;

    if (_policy is null)
    {
      return;
    }

    if (!_policy.IsBoundTo(sessionOptions.Value.SessionId))
    {
      _logger.LogCritical(
        "Command policy for session {SessionId} failed seal verification. All viewer commands for " +
        "this session will be dropped.",
        sessionOptions.Value.SessionId);

      _allowedDtoTypes = [];
      return;
    }

    _allowedDtoTypes = [.. _policy.AllowedDtoTypes];
  }

  public Task<Result> Handle(RemoteControlViewerWrapper wrapper)
  {
    if (_policy is null)
    {
      return _innerHandler.Handle(wrapper);
    }

    if (_allowedDtoTypes is null || !_allowedDtoTypes.Contains(wrapper.DtoType))
    {
      _logger.LogInformation(
        "Viewer command {DtoType} for session {SessionId} was dropped by the responder command " +
        "policy gate.",
        wrapper.DtoType,
        _policy.SessionId);

      return Task.FromResult(Result.Ok());
    }

    return _innerHandler.Handle(wrapper);
  }
}
