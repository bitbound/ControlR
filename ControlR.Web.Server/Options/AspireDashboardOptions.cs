namespace ControlR.Web.Server.Options;

public class AspireDashboardOptions
{
  public const string SectionKey = "AspireDashboard";

  public string? Token { get; init; }
  public Uri? WebBaseUrl { get; init; }
}
