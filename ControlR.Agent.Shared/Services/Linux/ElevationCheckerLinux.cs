using ControlR.Libraries.NativeInterop.Unix;

namespace ControlR.Agent.Shared.Services.Linux;

public class ElevationCheckerLinux : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerLinux();

  public bool IsElevated()
  {
    return Libc.Geteuid() == 0;
  }
}
