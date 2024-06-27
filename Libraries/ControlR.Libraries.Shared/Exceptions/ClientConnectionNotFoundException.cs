namespace ControlR.Libraries.Shared.Exceptions;

public class ClientConnectionNotFoundException(string _connectionId) : Exception
{
    public const string ErrorMessage = "The client connection does not exist.";
    public string ConnectionId => _connectionId;
    public override string Message => ErrorMessage;
}
