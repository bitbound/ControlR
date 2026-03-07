namespace ControlR.Agent.Common.Interfaces;

public interface IDesktopClientFileVerifier
{
  Result VerifyFile(string executablePath);
}
