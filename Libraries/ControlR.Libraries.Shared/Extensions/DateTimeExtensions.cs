namespace ControlR.Libraries.Shared.Extensions;
public static class DateTimeExtensions
{
  public static DateTimeOffset ToDateTimeOffset(this DateTime dateTime) => new(dateTime);
}
