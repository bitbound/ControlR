namespace ControlR.Libraries.Agent.Interfaces;

public interface IAgentInstaller
{
  Task Install(Uri? serverUri = null, Guid? tenantId = null, Guid[]? tags = null);

  Task Uninstall();
}