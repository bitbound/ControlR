using System.Diagnostics;

namespace ControlR.Web.Server.Diagnostics;
internal static class DefaultActivitySources
{
  public const string Name = "ControlR.Web.Server";
  public static readonly ActivitySource Instance = new(Name);
  public static Activity? StartActivity(string name) => Instance.StartActivity(name);
}
