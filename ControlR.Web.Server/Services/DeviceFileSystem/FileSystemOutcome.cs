namespace ControlR.Web.Server.Services.DeviceFileSystem;

/// <summary>
/// The outcome of one device file system operation. One shape covers every operation, so the
/// service never has to know which status code its caller answers with.
/// </summary>
/// <param name="Failure">
/// Which guard or agent condition ended the operation, or <see cref="FileSystemFailure.None" />
/// when it completed.
/// </param>
/// <param name="Reason">
/// Diagnostic text: the agent's own explanation for a
/// <see cref="FileSystemFailure.HubRejected" />, or the exception message for an
/// <see cref="FileSystemFailure.Unexpected" /> failure. Null otherwise. Whether a caller surfaces
/// this in its response is the caller's decision.
/// </param>
/// <param name="Value">
/// The operation's payload. Null whenever <paramref name="Failure" /> is not
/// <see cref="FileSystemFailure.None" />.
/// </param>
public sealed record FileSystemOutcome<TValue>(FileSystemFailure Failure, string? Reason, TValue? Value)
{
  public bool Succeeded => Failure is FileSystemFailure.None;
}
