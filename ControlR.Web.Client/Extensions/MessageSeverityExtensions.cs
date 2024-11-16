namespace ControlR.Web.Client.Extensions;

public static class MessageSeverityExtensions
{
  public static Severity ToMudSeverity(this MessageSeverity messageSeverity)
  {
    return messageSeverity switch
    {
      MessageSeverity.Information => Severity.Info,
      MessageSeverity.Success => Severity.Success,
      MessageSeverity.Warning => Severity.Warning,
      MessageSeverity.Error => Severity.Error,
      _ => Severity.Info
    };
  }
}
