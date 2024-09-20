namespace ControlR.Libraries.Shared.Exceptions;

/// <summary>
///   Thrown when a process exit with a non-zero status code.
/// </summary>
public class ProcessStatusException(int statusCode) : Exception
{
  public override string Message =>
    $"Process exited with status code {statusCode}";
}