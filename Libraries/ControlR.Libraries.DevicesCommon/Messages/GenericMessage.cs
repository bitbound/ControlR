namespace ControlR.Libraries.DevicesCommon.Messages;

public class GenericMessage<T>(T value)
    where T : notnull
{
    public T Value { get; } = value;
}