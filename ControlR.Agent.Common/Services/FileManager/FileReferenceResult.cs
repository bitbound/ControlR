using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Common.Services.FileManager;

public class FileReferenceResult : IDisposable
{

  private FileReferenceResult(
    bool isSuccess,
    string? errorMessage = null,
    string? fileSystemPath = null,
    Action? onDispose = null)
  {
    IsSuccess = isSuccess;
    ErrorMessage = errorMessage;
    FileSystemPath = fileSystemPath;
    OnDispose = onDispose;
  }

  public string? ErrorMessage { get; init; }

  public string? FileSystemPath { get; init; }

  [MemberNotNullWhen(true, nameof(FileSystemPath))]
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
  public static FileReferenceResult Ok(string fileSystemPath, Action? onDispose = null)
  {
    return new FileReferenceResult(
      isSuccess: true,
      fileSystemPath: fileSystemPath,
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
