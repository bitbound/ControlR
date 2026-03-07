using ControlR.Agent.Common.Interfaces;

namespace ControlR.Agent.Common.Services;

public class DesktopClientFileVerifierLinux : IDesktopClientFileVerifier
{
  public Result VerifyFile(string executablePath)
  {
    return Result.Ok();
  }
}
