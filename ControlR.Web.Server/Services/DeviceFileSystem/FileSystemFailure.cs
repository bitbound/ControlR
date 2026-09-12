namespace ControlR.Web.Server.Services.DeviceFileSystem;

/// <summary>
/// Why a device file system operation stopped before it produced a payload. The set describes the
/// guard or agent condition that ended the operation and carries no HTTP meaning: the endpoints
/// that reach the same failure do not answer it the same way, so each caller picks its own status.
/// </summary>
public enum FileSystemFailure
{
  /// <summary>
  /// The request reached the agent and the operation completed.
  /// </summary>
  None = 0,

  /// <summary>
  /// No device with the requested id was visible to the caller.
  /// </summary>
  DeviceNotFound,

  /// <summary>
  /// The caller does not hold the device resource policy the operation requires.
  /// </summary>
  Forbidden,

  /// <summary>
  /// The device record exists but the agent is not currently connected.
  /// </summary>
  DeviceOffline,

  /// <summary>
  /// Waiting on the agent was canceled, usually because the request timed out.
  /// </summary>
  Cancelled,

  /// <summary>
  /// The agent answered, and the answer was a rejection. <see cref="FileSystemOutcome{TValue}.Reason" />
  /// holds the agent's own explanation, or is null when the agent returned nothing at all.
  /// </summary>
  HubRejected,

  /// <summary>
  /// The dispatch threw something other than cancellation.
  /// </summary>
  Unexpected
}
