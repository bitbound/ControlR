namespace ControlR.Libraries.Api.Contracts.Settings;

public sealed record ParseResult<T>(bool IsSuccess, T Value)
{
  public static ParseResult<T> Failure(T defaultValue)
  {
    return new(false, defaultValue);
  }

  public static ParseResult<T> Success(T value)
  {
    return new(true, value);
  }
}
