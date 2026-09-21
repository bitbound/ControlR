using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Common.Services.FileManager;

public class FileReferenceResult
{

  private FileReferenceResult(
    bool isSuccess,
    OperationFailureCode code = OperationFailureCode.Unknown,
    string? errorMessage = null,
    string? fileSystemPath = null,
    string? fileDisplayName = null,
    bool isTempFile = false)
  {
    IsSuccess = isSuccess;
    Code = code;
    ErrorMessage = errorMessage;
    FileSystemPath = fileSystemPath;
    FileDisplayName = fileDisplayName;
    IsTempFile = isTempFile;
  }

  /// <summary>
  /// Machine-readable reason for a failure. <see cref="OperationFailureCode.Unknown" /> on a success.
  /// Same enum as HubResult.FailureCode on purpose; the agent sets it here and passes it to the hub result.
  /// </summary>
  public OperationFailureCode Code { get; init; }
  public string? ErrorMessage { get; init; }
  public string? FileDisplayName { get; init; }
  public string? FileSystemPath { get; init; }

  [MemberNotNullWhen(true, nameof(FileSystemPath))]
  [MemberNotNullWhen(true, nameof(FileDisplayName))]
  [MemberNotNullWhen(false, nameof(ErrorMessage))]
  public bool IsSuccess { get; init; }
  public bool IsTempFile { get; }

  public static FileReferenceResult Fail(string errorMessage, OperationFailureCode code)
  {
    return new FileReferenceResult(
      isSuccess: false,
      code: code,
      errorMessage: errorMessage);
  }

  public static FileReferenceResult Ok(string fileSystemPath, string displayName, bool isTempFile = false)
  {
    return new FileReferenceResult(
      isSuccess: true,
      fileSystemPath: fileSystemPath,
      fileDisplayName: displayName,
      isTempFile: isTempFile);
  }
}
