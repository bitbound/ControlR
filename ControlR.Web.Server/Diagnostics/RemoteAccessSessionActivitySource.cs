using System.Diagnostics;

namespace ControlR.Web.Server.Diagnostics;

public static class RemoteAccessSessionActivitySource
{
  public const string SourceName = "ControlR.Web.Server.RemoteAccess";

  private const string RemoteAccessSessionActivityName = "remote_access_session";

  public static readonly ActivitySource Instance = new(SourceName);

  public static Activity? StartRemoteAccessSession()
  {
    return Instance.StartActivity(
      RemoteAccessSessionActivityName, 
      ActivityKind.Internal, 
      parentId: null);
  }
}