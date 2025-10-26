using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Common.Services.FileManager;

public class FileReferenceResult : IDisposable
{

  private FileReferenceResult(
    bool isSuccess,
    string? errorMessage = null,
    string? fileSystemPath = null,
    string? fileDisplayName = null,
    Action? onDispose = null)
  {
    IsSuccess = isSuccess;
    ErrorMessage = errorMessage;
    FileSystemPath = fileSystemPath;
    FileDisplayName = fileDisplayName;
    OnDispose = onDispose;
  }

  public string? ErrorMessage { get; init; }

  public string? FileDisplayName { get; init; }

  public string? FileSystemPath { get; init; }

  [MemberNotNullWhen(true, nameof(FileSystemPath))]
  [MemberNotNullWhen(true, nameof(FileDisplayName))]
  [MemberNotNullWhen(false, nameof(ErrorMessage))]
  public bool IsSuccess { get; init; }

  public Action? OnDispose { get; }

  public static FileReferenceResult Fail(string errorMessage, Action? onDispose = null)
  {
    return new FileReferenceResult(
      isSuccess: false,
      errorMessage: errorMessage,
      onDispose: onDispose);
  }
  public static FileReferenceResult Ok(string fileSystemPath, string displayName, Action? onDispose = null)
  {
    return new FileReferenceResult(
      isSuccess: true,
      fileSystemPath: fileSystemPath,
      fileDisplayName: displayName,
      onDispose: onDispose);
  }

  public void Dispose()
  {
    try
    {
      OnDispose?.Invoke();
    }
    catch
    {
      // Ignore
    }
    GC.SuppressFinalize(this);
  }
}
