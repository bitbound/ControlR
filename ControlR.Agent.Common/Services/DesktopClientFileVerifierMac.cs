using ControlR.Agent.Common.Interfaces;

namespace ControlR.Agent.Common.Services;

public class DesktopClientFileVerifierMac : IDesktopClientFileVerifier
{
  public Result VerifyFile(string executablePath)
  {
    return Result.Ok();
  }
}
