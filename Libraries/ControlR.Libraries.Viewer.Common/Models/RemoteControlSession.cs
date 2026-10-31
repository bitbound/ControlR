using ControlR.Libraries.Api.Contracts.Dtos.Devices;

namespace ControlR.Libraries.Viewer.Common.Models;

public class RemoteControlSession
{
  private readonly DeviceResponseDto? _device;

  public RemoteControlSession(
    DeviceResponseDto device,
    int targetSystemSession,
    int targetProcessId,
    DesktopSessionType desktopSessionType)
  {
    _device = device;
    TargetSystemSession = targetSystemSession;
    TargetProcessId = targetProcessId;
    DesktopSessionType = desktopSessionType;
  }

  private RemoteControlSession(Guid sessionId)
  {
    SessionId = sessionId;
    IsRelayDirect = true;
  }

  public DesktopSessionType DesktopSessionType { get; }

  public DeviceResponseDto Device =>
    _device ?? throw new InvalidOperationException("This session has no managed device.");

  /// <summary>
  /// True when the peer has no agent to carry commands over the hub, so viewer DTOs are sent
  /// directly on the WebSocket relay instead. Assist sessions use this path.
  /// </summary>
  public bool IsRelayDirect { get; }

  public Guid SessionId { get; private set; } = Guid.NewGuid();

  public int TargetProcessId { get; }

  public int TargetSystemSession { get; }

  /// <summary>
  /// Creates a session whose peer is an assist responder that talks to the relay directly. The
  /// session id must match the one the assist control plane issued so both halves pair up.
  /// </summary>
  public static RemoteControlSession CreateForAssistSession(Guid sessionId) => new(sessionId);

  public void CreateNewSessionId()
  {
    SessionId = Guid.NewGuid();
  }
}
