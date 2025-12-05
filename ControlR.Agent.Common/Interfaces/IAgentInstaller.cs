namespace ControlR.Agent.Common.Interfaces;

public interface IAgentInstaller
{
  Task Install(
    Uri? serverUri = null, 
    Guid? tenantId = null, 
    string? installerKeySecret = null, 
    Guid? installerKeyId = null,
    Guid? deviceId = null,
    Guid[]? tags = null);

  Task Uninstall();
}