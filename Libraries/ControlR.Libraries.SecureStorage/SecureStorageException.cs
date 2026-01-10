namespace ControlR.Libraries.SecureStorage;

/// <summary>
/// Exception thrown when an error occurs with secure storage operations.
/// </summary>
public class SecureStorageException : Exception
{
    public SecureStorageException()
    {
    }

    public SecureStorageException(string message) : base(message)
    {
    }

    public SecureStorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
