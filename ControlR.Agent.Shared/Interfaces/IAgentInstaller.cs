using ControlR.Agent.Shared.Models;

namespace ControlR.Agent.Shared.Interfaces;

public interface IAgentInstaller
{
  Task Install(AgentInstallRequest request);

  Task RepairDesktopClient(AgentInstallRequest request);

  /// <summary>
  /// Removes this install's service, files, and uninstall registration.
  /// </summary>
  /// <param name="preserveMachinePolicy">
  /// When true, machine-wide policy values that this install enabled are left in place. A cross-brand
  /// migration passes this while retiring the old install, because the newly installed brand still
  /// depends on the same values and they are not tracked per install.
  /// </param>
  Task Uninstall(bool preserveMachinePolicy);
}
