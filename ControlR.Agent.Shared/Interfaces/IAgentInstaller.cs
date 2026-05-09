using ControlR.Agent.Shared.Models;

namespace ControlR.Agent.Shared.Interfaces;

public interface IAgentInstaller
{
  Task Install(AgentInstallRequest request);

  Task RepairDesktopClient(AgentInstallRequest request);

  Task Uninstall();
}
