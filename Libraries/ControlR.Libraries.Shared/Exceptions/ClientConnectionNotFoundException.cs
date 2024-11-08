namespace ControlR.Libraries.Shared.Exceptions;

public class ClientConnectionNotFoundException(string connectionId) : Exception
{
  private const string ErrorMessage = "The client connection does not exist.";
  public string ConnectionId => connectionId;
  public override string Message => ErrorMessage;
}