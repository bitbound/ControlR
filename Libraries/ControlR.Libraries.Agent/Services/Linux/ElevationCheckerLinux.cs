using ControlR.Devices.Native.Linux;
using ControlR.Libraries.Agent.Interfaces;

namespace ControlR.Libraries.Agent.Services.Linux;

public class ElevationCheckerLinux : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerLinux();

  public bool IsElevated()
  {
    return Libc.Geteuid() == 0;
  }
}