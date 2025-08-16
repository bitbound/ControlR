using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.Agent.Common.Services.Linux;

public class ElevationCheckerLinux : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerLinux();

  public bool IsElevated()
  {
    return Libc.Geteuid() == 0;
  }
}