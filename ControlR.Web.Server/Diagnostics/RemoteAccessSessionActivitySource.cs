using System.Diagnostics;

namespace ControlR.Web.Server.Diagnostics;

public static class RemoteAccessSessionActivitySource
{
  public const string SourceName = "ControlR.RemoteAccess";

  private const string RemoteAccessSessionActivityName = "RemoteAccessSession";

  public static readonly ActivitySource Instance = new(SourceName);

  public static Activity? StartRemoteAccessSession()
  {
    return Instance.StartActivity(
      RemoteAccessSessionActivityName, 
      ActivityKind.Internal, 
      parentId: null);
  }
}