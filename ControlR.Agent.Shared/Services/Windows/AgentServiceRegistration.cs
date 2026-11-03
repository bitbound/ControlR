using System.Runtime.Versioning;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Agent.Shared.Services.Windows;

/// <summary>
/// The Windows service registration itself, read and rewritten without going through the service
/// control manager's start and stop paths.
/// </summary>
[SupportedOSPlatform("windows")]
public interface IAgentServiceRegistration
{
  /// <summary>
  /// Points an existing service at a new executable and its run arguments, leaving every other part of
  /// the registration, including its failure actions, as it is. Used instead of delete and create because
  /// a deleted service stays in pending-delete until its last handle closes, and a create issued in that
  /// window fails.
  /// </summary>
  Task<Result> Repoint(string serviceName, string agentExecutablePath, string runArguments);

  /// <summary>
  /// Returns the directory the service is registered to run from, or null when no service of that name
  /// is registered. This is the only machine state that records where an installed agent's files are, so
  /// a caller that has to act on an existing install cannot substitute the path formula it computes.
  /// </summary>
  string? TryGetRegisteredDirectory(string serviceName);
}

[SupportedOSPlatform("windows")]
internal sealed class AgentServiceRegistration(
  IProcessManager processes,
  IRegistryAccessor registryAccessor,
  ILogger<AgentServiceRegistration> logger) : IAgentServiceRegistration
{
  public async Task<Result> Repoint(string serviceName, string agentExecutablePath, string runArguments)
  {
    using var _ = logger.BeginMemberScope();

    logger.LogInformation("Repointing the {ServiceName} service at {AgentExecutablePath}.", serviceName, agentExecutablePath);

    var configString = $"sc.exe config \"{serviceName}\" binPath= \"\\\"{agentExecutablePath}\\\" {runArguments}\"";
    var result = await processes.GetProcessOutput("cmd.exe", $"/c {configString}");
    if (!result.IsSuccess)
    {
      logger.LogError("Failed to repoint the {ServiceName} service. Reason: {Reason}", serviceName, result.Reason);
      return result.ToResult();
    }

    return Result.Ok();
  }

  public string? TryGetRegisteredDirectory(string serviceName)
  {
    var imagePath = registryAccessor.GetServiceImagePath(serviceName);
    if (string.IsNullOrWhiteSpace(imagePath))
    {
      return null;
    }

    var executablePath = TryGetExecutablePath(imagePath);
    return executablePath is null
      ? null
      : GetDirectory(executablePath);
  }

  // Windows separators are searched by hand rather than through Path, because the code that reads a
  // registration written on Windows also runs on a build host where Path does not treat a backslash as
  // a separator.
  private static string GetDirectory(string filePath)
  {
    var trimmed = filePath.TrimEnd('\\');
    var lastSeparator = trimmed.LastIndexOf('\\');
    return lastSeparator <= 0 ? trimmed : trimmed[..lastSeparator];
  }

  /// <summary>
  /// Reads the executable out of a registered <c>ImagePath</c>, which holds the run arguments after the
  /// path and quotes the path when it contains spaces.
  /// </summary>
  private static string? TryGetExecutablePath(string imagePath)
  {
    var trimmed = imagePath.TrimStart();

    if (trimmed.StartsWith('"'))
    {
      var closingQuote = trimmed.IndexOf('"', 1);
      return closingQuote > 0 ? trimmed[1..closingQuote] : null;
    }

    var firstSpace = trimmed.IndexOf(' ');
    return firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
  }
}
