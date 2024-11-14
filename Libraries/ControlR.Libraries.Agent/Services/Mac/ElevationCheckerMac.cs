using ControlR.Libraries.Agent.Interfaces;
using ControlR.Libraries.DevicesNative.Linux;

namespace ControlR.Libraries.Agent.Services.Mac;

public class ElevationCheckerMac : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerMac();

  public bool IsElevated()
  {
    return Libc.Geteuid() == 0;
  }
}