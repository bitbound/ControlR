using ControlR.Agent.Shared.Models;

namespace ControlR.Agent.Shared.Interfaces;

public interface IAgentInstaller
{
  /// <summary>
  /// Installs this build. When <see cref="AgentInstallRequest.PreviousBrandName"/> is set, the install
  /// being replaced is stopped before this one starts, and the returned result says whether this
  /// install's own service came up.
  /// </summary>
  Task<Result> Install(AgentInstallRequest request);

  Task RepairDesktopClient(AgentInstallRequest request);

  /// <summary>
  /// Starts the service of the install being replaced, to roll back a migration that could not start.
  /// </summary>
  Task<Result> RestorePreviousBrand(string previousBrandName, string? previousInstanceId);

  /// <summary>
  /// Removes the install being replaced, once this one is running.
  /// </summary>
  Task<Result> RetirePreviousBrand(string previousBrandName, string? previousInstanceId);

  /// <summary>
  /// Removes this install's service, files, and uninstall registration.
  /// </summary>
  Task Uninstall();
}
