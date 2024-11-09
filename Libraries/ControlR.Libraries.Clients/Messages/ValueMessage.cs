namespace ControlR.Libraries.Clients.Messages;

public class ValueMessage<T>(T value)
    where T : notnull
{
  public T Value { get; } = value;
}