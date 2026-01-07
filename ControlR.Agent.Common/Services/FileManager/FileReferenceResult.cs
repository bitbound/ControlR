using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Common.Services.FileManager;

public class FileReferenceResult
{

  private FileReferenceResult(
    bool isSuccess,
    string? errorMessage = null,
    string? fileSystemPath = null,
    string? fileDisplayName = null,
    bool isTempFile = false)
  {
    IsSuccess = isSuccess;
    ErrorMessage = errorMessage;
    FileSystemPath = fileSystemPath;
    FileDisplayName = fileDisplayName;
    IsTempFile = isTempFile;
  }

  public string? ErrorMessage { get; init; }
  public string? FileDisplayName { get; init; }
  public string? FileSystemPath { get; init; }

  [MemberNotNullWhen(true, nameof(FileSystemPath))]
  [MemberNotNullWhen(true, nameof(FileDisplayName))]
  [MemberNotNullWhen(false, nameof(ErrorMessage))]
  public bool IsSuccess { get; init; }
  public bool IsTempFile { get; }

  public static FileReferenceResult Fail(string errorMessage)
  {
    return new FileReferenceResult(
      isSuccess: false,
      errorMessage: errorMessage);
  }

  public static FileReferenceResult Ok(string fileSystemPath, string displayName, bool isTempFile = false)
  {
    return new FileReferenceResult(
      isSuccess: true,
      fileSystemPath: fileSystemPath,
      fileDisplayName: displayName);
  }
}
