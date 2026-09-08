using System.Runtime.Versioning;
using System.Security.Principal;

namespace ControlR.Agent.Shared.Services.Windows;

[SupportedOSPlatform("windows")]
public class ElevationCheckerWin : IElevationChecker
{
  public static IElevationChecker Instance { get; } = new ElevationCheckerWin();

  public bool IsElevated()
  {
    var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
  }
}
