using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.Agent.Shared.Services.Mac;

public class ElevationCheckerMac : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerMac();

  public bool IsElevated()
  {
    return Libc.Geteuid() == 0;
  }
}
