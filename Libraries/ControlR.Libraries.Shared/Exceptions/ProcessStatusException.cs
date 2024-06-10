namespace ControlR.Libraries.Shared.Exceptions;

/// <summary>
/// Thrown when a process exit with a non-zero status code.
/// </summary>
public class ProcessStatusException(int _statusCode) : Exception
{
    public int StatusCode { get; } = _statusCode;

    public override string Message =>
        $"Process exited with status code {StatusCode}";
}
