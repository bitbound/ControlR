using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Common.Services;

public class DesktopClientFileVerifierLinux : IDesktopClientFileVerifier
{
  public Result VerifyFile(string executablePath)
  {
    return Result.Ok();
  }
}
