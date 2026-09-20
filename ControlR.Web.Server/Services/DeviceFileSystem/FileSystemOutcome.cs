namespace ControlR.Web.Server.Services.DeviceFileSystem;

using ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// The outcome of a value-less device file system operation, such as creating a directory or
/// deleting a path. Only why the operation stopped is reported, because there is no payload.
/// </summary>
/// <param name="Failure">
/// Which guard or agent condition ended the operation, or <see cref="FileSystemFailure.None" />
/// when it completed.
/// </param>
/// <param name="Reason">
/// Diagnostic text: the agent's own explanation for a
/// <see cref="FileSystemFailure.RemoteFailure" />, or the exception message for an
/// <see cref="FileSystemFailure.Unexpected" /> failure. Null otherwise. Whether a caller surfaces
/// this in its response is the caller's decision.
/// </param>
public record FileSystemOutcome(FileSystemFailure Failure, string? Reason)
{
  /// <summary>
  /// Machine-readable reason for an agent
  /// <see cref="FileSystemFailure.RemoteFailure" />. <see cref="OperationFailureCode.Unknown" />
  /// otherwise, or from an agent that predates the field.
  /// </summary>
  public OperationFailureCode Code { get; init; } = OperationFailureCode.Unknown;

  public bool Succeeded => Failure is FileSystemFailure.None;
}

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
/// <see cref="FileSystemFailure.RemoteFailure" />, or the exception message for an
/// <see cref="FileSystemFailure.Unexpected" /> failure. Null otherwise. Whether a caller surfaces
/// this in its response is the caller's decision.
/// </param>
/// <param name="Value">
/// The operation's payload. Null whenever <paramref name="Failure" /> is not
/// <see cref="FileSystemFailure.None" />.
/// </param>
public sealed record FileSystemOutcome<TValue>(FileSystemFailure Failure, string? Reason, TValue? Value)
  : FileSystemOutcome(Failure, Reason);

