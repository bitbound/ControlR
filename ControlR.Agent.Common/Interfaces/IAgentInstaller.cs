namespace ControlR.Agent.Common.Interfaces;

public interface IAgentInstaller
{
  Task Install(Uri? serverUri = null, Guid? tenantId = null, string? installerKey = null, Guid[]? tags = null);

  Task Uninstall();
}