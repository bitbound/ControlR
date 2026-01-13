using ControlR.Libraries.DataRedaction;

namespace ControlR.Web.Server.Options;

public class AspireDashboardOptions
{
  public const string SectionKey = "AspireDashboard";

  public Uri? PublicWebUrl { get; init; }
  
  [ProtectedDataClassification]
  public string? Token { get; init; }
}
